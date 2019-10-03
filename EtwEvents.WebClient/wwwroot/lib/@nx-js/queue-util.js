var priorities = {
  SYNC: 0,
  CRITICAL: 1,
  HIGH: 2,
  LOW: 3
};

var queues = {};
queues[priorities.SYNC] = [];
queues[priorities.CRITICAL] = [];
queues[priorities.HIGH] = [];
queues[priorities.LOW] = [];

var tickTask; // tick means the next microtask
var rafTask; // raf means the next animation frame (requestAnimationFrame)
var ricTask; // ric means the next idle perdiod (requestIdleCallback)

var currentTick = Promise.resolve();

function getRaf() {
  return typeof requestAnimationFrame === 'function' ? requestAnimationFrame : setTimeout;
}

function getRic() {
  return typeof requestIdleCallback === 'function' ? requestIdleCallback : getRaf();
}

// schedule a tick task, if it is not yet scheduled
function nextTick(task) {
  if (!tickTask) {
    tickTask = task;
    currentTick.then(runTickTask);
  }
}

function runTickTask() {
  var task = tickTask;
  // set the task to undefined BEFORE calling it
  // this allows it to re-schedule itself for a later time
  tickTask = undefined;
  task();
}

// schedule a raf task, if it is not yet scheduled
function nextAnimationFrame(task) {
  if (!rafTask) {
    rafTask = task;
    var raf = getRaf();
    raf(runRafTask);
  }
}

function runRafTask() {
  var task = rafTask;
  // set the task to undefined BEFORE calling it
  // this allows it to re-schedule itself for a later time
  rafTask = undefined;
  task();
}

// schedule a ric task, if it is not yet scheduled
function nextIdlePeriod(task) {
  if (!ricTask) {
    ricTask = task;
    var ric = getRic();
    ric(runRicTask);
  }
}

function runRicTask() {
  // do not run ric task if there are pending raf tasks
  // let the raf tasks execute first and schedule the ric task for later
  if (!rafTask) {
    var task = ricTask;
    // set the task to undefined BEFORE calling it
    // this allows it to re-schedule itself for a later time
    ricTask = undefined;
    task();
  } else {
    var ric = getRic();
    ric(runRicTask);
  }
}

var TARGET_FPS = 60;
var TARGET_INTERVAL = 1000 / TARGET_FPS;

function queueTaskProcessing(priority) {
  if (priority === priorities.CRITICAL) {
    nextTick(runQueuedCriticalTasks);
  } else if (priority === priorities.HIGH) {
    nextAnimationFrame(runQueuedHighTasks);
  } else if (priority === priorities.LOW) {
    nextIdlePeriod(runQueuedLowTasks);
  }
}

function runQueuedCriticalTasks() {
  // critical tasks must all execute before the next frame
  var criticalQueues = queues[priorities.CRITICAL];
  criticalQueues.forEach(processCriticalQueue);
}

function processCriticalQueue(queue) {
  queue.forEach(runTask);
  queue.clear();
}

function runQueuedHighTasks() {
  var startTime = Date.now();
  var isEmpty = processIdleQueues(priorities.HIGH, startTime);
  // there are more tasks to run in the next cycle
  if (!isEmpty) {
    nextAnimationFrame(runQueuedHighTasks);
  }
}

function runQueuedLowTasks() {
  var startTime = Date.now();
  var isEmpty = processIdleQueues(priorities.LOW, startTime);
  // there are more tasks to run in the next cycle
  if (!isEmpty) {
    nextIdlePeriod(runQueuedLowTasks);
  }
}

function processIdleQueues(priority, startTime) {
  var idleQueues = queues[priority];
  var isEmpty = true;

  // if a queue is not empty after processing, it means we have no more time
  // the loop whould stop in this case
  for (var i = 0; isEmpty && i < idleQueues.length; i++) {
    var queue = idleQueues.shift();
    isEmpty = isEmpty && processIdleQueue(queue, startTime);
    idleQueues.push(queue);
  }
  return isEmpty;
}

function processIdleQueue(queue, startTime) {
  var iterator = queue[Symbol.iterator]();
  var task = iterator.next();
  while (Date.now() - startTime < TARGET_INTERVAL) {
    if (task.done) {
      return true;
    }
    runTask(task.value);
    queue.delete(task.value);
    task = iterator.next();
  }
}

function runTask(task) {
  task();
}

var QUEUE = Symbol('task queue');
var IS_STOPPED = Symbol('is stopped');
var IS_SLEEPING = Symbol('is sleeping');

var Queue = function Queue(priority) {
  if ( priority === void 0 ) priority = priorities.SYNC;

  this[QUEUE] = new Set();
  this.priority = priority;
  queues[this.priority].push(this[QUEUE]);
};

var prototypeAccessors = { size: {} };

Queue.prototype.has = function has (task) {
  return this[QUEUE].has(task);
};

Queue.prototype.add = function add (task) {
  if (this[IS_SLEEPING]) {
    return;
  }
  if (this.priority === priorities.SYNC && !this[IS_STOPPED]) {
    task();
  } else {
    var queue = this[QUEUE];
    queue.add(task);
  }
  if (!this[IS_STOPPED]) {
    queueTaskProcessing(this.priority);
  }
};

Queue.prototype.delete = function delete$1 (task) {
  this[QUEUE].delete(task);
};

Queue.prototype.start = function start () {
  var queue = this[QUEUE];
  if (this.priority === priorities.SYNC) {
    this.process();
  } else {
    var priorityQueues = queues[this.priority];
    if (priorityQueues.indexOf(queue) === -1) {
      priorityQueues.push(queue);
    }
    queueTaskProcessing(this.priority);
  }
  this[IS_STOPPED] = false;
  this[IS_SLEEPING] = false;
};

Queue.prototype.stop = function stop () {
  var queue = this[QUEUE];
  var priorityQueues = queues[this.priority];
  var index = priorityQueues.indexOf(queue);
  if (index !== -1) {
    priorityQueues.splice(index, 1);
  }
  this[IS_STOPPED] = true;
};

Queue.prototype.sleep = function sleep () {
  this.stop();
  this[IS_SLEEPING] = true;
};

Queue.prototype.clear = function clear () {
  this[QUEUE].clear();
};

prototypeAccessors.size.get = function () {
  return this[QUEUE].size;
};

Queue.prototype.process = function process () {
  var queue = this[QUEUE];
  queue.forEach(runTask);
  queue.clear();
};

Queue.prototype.processing = function processing () {
  var queue = this[QUEUE];
  return new Promise(function (resolve) {
    if (queue.size === 0) {
      resolve();
    } else {
      queue.add(resolve);
    }
  });
};

Object.defineProperties( Queue.prototype, prototypeAccessors );

export { Queue, priorities };
