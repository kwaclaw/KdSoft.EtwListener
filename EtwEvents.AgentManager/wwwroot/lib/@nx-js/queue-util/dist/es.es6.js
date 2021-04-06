const priorities = {
  SYNC: 0,
  CRITICAL: 1,
  HIGH: 2,
  LOW: 3
};

const queues = {
  [priorities.SYNC]: [],
  [priorities.CRITICAL]: [],
  [priorities.HIGH]: [],
  [priorities.LOW]: []
};

let tickTask; // tick means the next microtask
let rafTask; // raf means the next animation frame (requestAnimationFrame)
let ricTask; // ric means the next idle perdiod (requestIdleCallback)

const currentTick = Promise.resolve();

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
  const task = tickTask;
  // set the task to undefined BEFORE calling it
  // this allows it to re-schedule itself for a later time
  tickTask = undefined;
  task();
}

// schedule a raf task, if it is not yet scheduled
function nextAnimationFrame(task) {
  if (!rafTask) {
    rafTask = task;
    const raf = getRaf();
    raf(runRafTask);
  }
}

function runRafTask() {
  const task = rafTask;
  // set the task to undefined BEFORE calling it
  // this allows it to re-schedule itself for a later time
  rafTask = undefined;
  task();
}

// schedule a ric task, if it is not yet scheduled
function nextIdlePeriod(task) {
  if (!ricTask) {
    ricTask = task;
    const ric = getRic();
    ric(runRicTask);
  }
}

function runRicTask() {
  // do not run ric task if there are pending raf tasks
  // let the raf tasks execute first and schedule the ric task for later
  if (!rafTask) {
    const task = ricTask;
    // set the task to undefined BEFORE calling it
    // this allows it to re-schedule itself for a later time
    ricTask = undefined;
    task();
  } else {
    const ric = getRic();
    ric(runRicTask);
  }
}

const TARGET_FPS = 60;
const TARGET_INTERVAL = 1000 / TARGET_FPS;

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
  const criticalQueues = queues[priorities.CRITICAL];
  criticalQueues.forEach(processCriticalQueue);
}

function processCriticalQueue(queue) {
  queue.forEach(runTask);
  queue.clear();
}

function runQueuedHighTasks() {
  const startTime = Date.now();
  const isEmpty = processIdleQueues(priorities.HIGH, startTime);
  // there are more tasks to run in the next cycle
  if (!isEmpty) {
    nextAnimationFrame(runQueuedHighTasks);
  }
}

function runQueuedLowTasks() {
  const startTime = Date.now();
  const isEmpty = processIdleQueues(priorities.LOW, startTime);
  // there are more tasks to run in the next cycle
  if (!isEmpty) {
    nextIdlePeriod(runQueuedLowTasks);
  }
}

function processIdleQueues(priority, startTime) {
  const idleQueues = queues[priority];
  let isEmpty = true;

  // if a queue is not empty after processing, it means we have no more time
  // the loop whould stop in this case
  for (let i = 0; isEmpty && i < idleQueues.length; i++) {
    const queue = idleQueues.shift();
    isEmpty = isEmpty && processIdleQueue(queue, startTime);
    idleQueues.push(queue);
  }
  return isEmpty;
}

function processIdleQueue(queue, startTime) {
  const iterator = queue[Symbol.iterator]();
  let task = iterator.next();
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

const QUEUE = Symbol('task queue');
const IS_STOPPED = Symbol('is stopped');
const IS_SLEEPING = Symbol('is sleeping');

class Queue {
  constructor(priority = priorities.SYNC) {
    this[QUEUE] = new Set();
    this.priority = priority;
    queues[this.priority].push(this[QUEUE]);
  }

  has(task) {
    return this[QUEUE].has(task);
  }

  add(task) {
    if (this[IS_SLEEPING]) {
      return;
    }
    if (this.priority === priorities.SYNC && !this[IS_STOPPED]) {
      task();
    } else {
      const queue = this[QUEUE];
      queue.add(task);
    }
    if (!this[IS_STOPPED]) {
      queueTaskProcessing(this.priority);
    }
  }

  delete(task) {
    this[QUEUE].delete(task);
  }

  start() {
    const queue = this[QUEUE];
    if (this.priority === priorities.SYNC) {
      this.process();
    } else {
      const priorityQueues = queues[this.priority];
      if (priorityQueues.indexOf(queue) === -1) {
        priorityQueues.push(queue);
      }
      queueTaskProcessing(this.priority);
    }
    this[IS_STOPPED] = false;
    this[IS_SLEEPING] = false;
  }

  stop() {
    const queue = this[QUEUE];
    const priorityQueues = queues[this.priority];
    const index = priorityQueues.indexOf(queue);
    if (index !== -1) {
      priorityQueues.splice(index, 1);
    }
    this[IS_STOPPED] = true;
  }

  sleep() {
    this.stop();
    this[IS_SLEEPING] = true;
  }

  clear() {
    this[QUEUE].clear();
  }

  get size() {
    return this[QUEUE].size;
  }

  process() {
    const queue = this[QUEUE];
    queue.forEach(runTask);
    queue.clear();
  }

  processing() {
    const queue = this[QUEUE];
    return new Promise(resolve => {
      if (queue.size === 0) {
        resolve();
      } else {
        queue.add(resolve);
      }
    });
  }
}

export { Queue, priorities };
