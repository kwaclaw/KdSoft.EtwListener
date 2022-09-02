using System;
using KdSoft.EtwEvents.AgentManager;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KdSoft.EtwEvents.WebClient.Pages
{
    public class IndexModel: PageModel
    {
        readonly IOptionsMonitor<AuthorizationOptions> _authOpts;
        readonly ILogger<IndexModel> _logger;

        public IndexModel(IOptionsMonitor<AuthorizationOptions> authOpts, ILogger<IndexModel> logger) {
            _authOpts = authOpts;
            _logger = logger;
        }

        public void OnGet() {

        }

        public TimeSpan? ClientCertLifeSpan {
            get {
                var expiryDate = HttpContext.Connection.ClientCertificate?.NotAfter;
                if (expiryDate != null) {
                    return expiryDate.Value - DateTime.Now;
                }
                return null;
            }
        }

        public int CertExpiryWarningDays => _authOpts.CurrentValue.CertExpiryWarningDays;

        //public string ResourceBase64() {
        //    // Retrieves the requested culture
        //    var rqf = Request.HttpContext.Features.Get<IRequestCultureFeature>();
        //    var culture = rqf.RequestCulture.Culture;
        //    var rs = Resource.ResourceManager.GetResourceSet(culture, true, true);
        //    var resourceBytes = rs != null ? JsonSerializer.SerializeToUtf8Bytes<ResourceSet>(rs) : Array.Empty<byte>();
        //    return Convert.ToBase64String(resourceBytes);
        //}
    }
}
