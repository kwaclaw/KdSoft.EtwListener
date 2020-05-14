using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace KdSoft.EtwEvents.WebClient
{
    class TraceSessionChangeNotifier
    {
        readonly Func<Task<Models.TraceSessionStates>> _getSessionStates;

        public TraceSessionChangeNotifier(Func<Task<Models.TraceSessionStates>> getSessionStates) {
            this._getSessionStates = getSessionStates;
        }

        public async ValueTask PostSessionStateChange() {
            var changeEnumerators = _changeEnumerators;
            foreach (var enumerator in changeEnumerators) {
                try {
                    await enumerator.Advance().ConfigureAwait(false);
                }
                catch { }
            }
        }

        readonly object _enumeratorSync = new object();
        ImmutableList<ChangeEnumerator> _changeEnumerators = ImmutableList<ChangeEnumerator>.Empty;

        void AddEnumerator(ChangeEnumerator enumerator) {
            lock (_enumeratorSync) {
                _changeEnumerators = _changeEnumerators.Add(enumerator);
            }
        }

        void RemoveEnumerator(ChangeEnumerator enumerator) {
            lock (_enumeratorSync) {
                _changeEnumerators = _changeEnumerators.Remove(enumerator);
            }
        }

        public IAsyncEnumerable<Models.TraceSessionStates> GetSessionStateChanges() {
            return new ChangeListener(this);
        }

        // https://anthonychu.ca/post/async-streams-dotnet-core-3-iasyncenumerable/
        class ChangeListener: IAsyncEnumerable<Models.TraceSessionStates>
        {
            readonly TraceSessionChangeNotifier _changeNotifier;

            public ChangeListener(TraceSessionChangeNotifier changeNotifier) {
                this._changeNotifier = changeNotifier;
            }

            public IAsyncEnumerator<Models.TraceSessionStates> GetAsyncEnumerator(CancellationToken cancellationToken = default) {
                return new ChangeEnumerator(_changeNotifier, cancellationToken);
            }
        }

        // https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-8.0/async-streams
        class ChangeEnumerator: PendingAsyncEnumerator<Models.TraceSessionStates>
        {
            readonly TraceSessionChangeNotifier _changeNotifier;

            public ChangeEnumerator(TraceSessionChangeNotifier changeNotifier, CancellationToken cancelToken) : base(cancelToken) {
                this._changeNotifier = changeNotifier;
                changeNotifier.AddEnumerator(this);
            }

            public override ValueTask DisposeAsync() {
                _changeNotifier.RemoveEnumerator(this);
                return default;
            }

            protected override Task<Models.TraceSessionStates> GetNext() {
                return _changeNotifier._getSessionStates();
            }
        }
    }
}
