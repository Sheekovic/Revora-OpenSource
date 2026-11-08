using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace Revora.XScape
{
    /// <summary> 
    /// A delegate used in the thread. 
    /// </summary> 
    public delegate void ThreadAction();

    internal class DelayedAction
    {
        internal DateTime allowedTime;
        internal ThreadAction threadAction;
        internal uint actionID;
        internal int repeat;
        internal int repeated = 0;
    }

    /// <summary> 
    /// This class is used to handle delayed tasks. 
    /// </summary> 
    public class DelayedTask
    {
        private IDisposable Subscribe;

        /// <summary> 
        /// The collection of all the threading objects and their tasks. 
        /// </summary> 
        private ConcurrentDictionary<uint, DelayedAction> taskObjects;

        static uint TaskID = 0;
        /// <summary> 
        /// The synchronization root. 
        /// </summary> 
        private static object SyncRoot;

        public DelayedTask()
        {
            System.Threading.Interlocked.CompareExchange(ref SyncRoot, new object(), null);

            taskObjects = new ConcurrentDictionary<uint, DelayedAction>();
            Subscribe = Thread.Subscribe(Work, 0);

        }
        private void Work(int time)
        {
            foreach (DelayedAction action in taskObjects.Values)
            {
                try
                {
                    action.threadAction.Invoke();
                }
                catch (Exception e) { Console.WriteLine(e.ToString()); }
            }
        }

        /// <summary> 
        /// Starting a delayed task. 
        /// </summary> 
        /// <param name="delayedAction">The delayed action.</param> 
        /// <param name="waitTime">The timespan before invoking the action. (Milliseconds.)</param> 
        /// <param name="repeat">The amount of times the action should be repeated.</param> 
        public uint StartDelayedTask(ThreadAction delayedAction, int waitTime, int repeat = 0, int reapeatdelay = 0)
        {
            DelayedAction taskAction = new DelayedAction();
            lock (SyncRoot)
            {
                taskAction.actionID = TaskID;
                TaskID++;
            }

            taskAction.allowedTime = DateTime.Now.AddMilliseconds(waitTime);
            taskAction.repeat = repeat;
            taskAction.threadAction = new ThreadAction(() =>
            {
                if (DateTime.Now >= taskAction.allowedTime)
                {
                    delayedAction.Invoke();

                    if (taskAction.repeated >= taskAction.repeat)
                    {
                        DelayedAction outDA;
                        taskObjects.TryRemove(taskAction.actionID, out outDA);
                    }
                    else
                        taskAction.allowedTime = DateTime.Now.AddMilliseconds(reapeatdelay);

                    taskAction.repeated++;
                }
            });
            taskObjects.TryAdd(taskAction.actionID, taskAction);
            return taskAction.actionID;
        }

        public void Remove(uint actionID)
        {
            DelayedAction raction;
            taskObjects.TryRemove(actionID, out raction);
        }
    }
}