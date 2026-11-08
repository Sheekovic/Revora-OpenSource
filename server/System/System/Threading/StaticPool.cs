// * Created by AliMOssaed
// * Copyright © 2015
// * Epic-Fight - Project
namespace System.Threading
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Security;
    using System.Threading.Generic;

    public class StaticPool : IDisposable
    {
        internal volatile bool bool_0;
        internal volatile bool bool_1;
        internal Dictionary<int, Class2> dictionary_0;
        internal int int_0;
        internal int int_1;
        internal int int_2;
        internal int int_3;
        internal int int_4;
        internal List<Thread> list_0;
        internal object object_0;
        internal object object_1;
        protected internal Thread propagationThread;
        internal Queue<Class2> queue_0;
        public const int SleepTime = 1;

        public StaticPool(int maximumPoolSize = 0x20)
        {
            //   Class1.Class0.smethod_0();
            this.bool_1 = false;
            this.object_1 = new object();
            this.object_0 = new object();
            this.dictionary_0 = new Dictionary<int, Class2>();
            this.queue_0 = new Queue<Class2>();
            this.list_0 = new List<Thread>();
            this.int_3 = maximumPoolSize;
            this.int_4 = maximumPoolSize;
        }

        public void Clear()
        {
            lock (this.object_0)
            {
                this.queue_0.Clear();
            }
        }

        ~StaticPool()
        {
            this.method_1(false);
        }

        [SecuritySafeCritical]
        internal void method_0()
        {
            if (!this.bool_1)
            {
                Interlocked.Increment(ref this.int_0);
                Interlocked.Increment(ref this.int_1);
                Thread item = new Thread(new ThreadStart(this.method_2), 0x100000);
                this.list_0.Add(item);
                item.Priority = ThreadPriority.Normal;
                item.Start();
            }
        }

        internal void method_1(bool bool_2)
        {
            if (!this.bool_1)
            {
                this.bool_1 = true;
                this.bool_0 = false;
                if (bool_2)
                {
                    foreach (Thread thread in this.list_0)
                    {
                        thread.Abort();
                    }
                }
                this.dictionary_0.Clear();
                this.dictionary_0 = null;
                this.queue_0 = null;
                this.list_0 = null;
            }
        }

        internal void method_2()
        {
            Thread currentThread = Thread.CurrentThread;
            while (this.bool_0)
            {
                Class2 class2;
                Thread.Sleep(1);
                if (this.method_3(out class2))
                {
                    if (class2.bool_0)
                    {
                        Interlocked.Decrement(ref this.int_1);
                        Interlocked.Increment(ref this.int_2);
                        currentThread.Priority = class2.vmethod_3();
                        try
                        {
                            class2.vmethod_0();
                        }
                        catch (Exception exception)
                        {
                            Console.WriteLine(exception);
                        }
                        finally
                        {
                            class2.bool_1 = false;
                        }
                        currentThread.Priority = ThreadPriority.Normal;
                        Interlocked.Decrement(ref this.int_2);
                        Interlocked.Increment(ref this.int_1);
                    }
                    else
                    {
                        this.method_4(class2.GetHashCode());
                    }
                }
            }
            Interlocked.Decrement(ref this.int_1);
        }

        internal bool method_3(out Class2 class2_0)
        {
            class2_0 = null;
            lock (this.object_0)
            {
                if (this.queue_0.Count != 0)
                {
                    Class2 class2 = this.queue_0.Dequeue();
                    class2_0 = class2;
                }
            }
            return (class2_0 != null);
        }

        internal void method_4(int int_5)
        {
            lock (this.object_1)
            {
                this.dictionary_0.Remove(int_5);
            }
        }

        private void method_5()
        {
            while (this.bool_0)
            {
                Queue<Class2> queue = new Queue<Class2>();
                Queue<int> queue2 = new Queue<int>();
                lock (this.object_1)
                {
                    foreach (Class2 class2 in this.dictionary_0.Values)
                    {
                        if (class2.bool_0)
                        {
                            if (!class2.bool_1 && class2.method_0())
                            {
                                class2.bool_1 = true;
                                queue.Enqueue(class2);
                            }
                        }
                        else
                        {
                            queue2.Enqueue(class2.GetHashCode());
                        }
                    }
                    while (queue2.Count != 0)
                    {
                        this.dictionary_0.Remove(queue2.Dequeue());
                    }
                }
                if (queue.Count != 0)
                {
                    lock (this.object_0)
                    {
                        while (queue.Count != 0)
                        {
                            this.queue_0.Enqueue(queue.Dequeue());
                        }
                    }
                }
                Thread.Sleep(1);
            }
        }

        public StaticPool Run()
        {
            this.bool_0 = true;
            for (int i = 0; i < this.int_3; i++)
            {
                this.method_0();
            }
            this.propagationThread = new Thread(new ThreadStart(this.method_5));
            this.propagationThread.Start();
            return this;
        }

        public IDisposable Subscribe(TimerRule instruction)
        {
            Class2 class2 = null;
            lock (this.object_1)
            {
                class2 = new Class4(instruction);
                if (instruction is LazyDelegate)
                {
                    class2.method_1(instruction.int_0);
                }
                this.dictionary_0[class2.GetHashCode()] = class2;
            }
            return class2;
        }

        public IDisposable Subscribe<T>(TimerRule<T> instruction, T param)
        {
            Class2 class2 = null;
            lock (this.object_1)
            {
                class2 = new Class3<T>(instruction, param);
                if (instruction is LazyDelegate<T>)
                {
                    class2.method_1(instruction.int_0);
                }
                this.dictionary_0[class2.GetHashCode()] = class2;
            }
            return class2;
        }

        void IDisposable.Dispose()
        {
            this.method_1(true);
        }

        public override string ToString()
        {
            int count = this.dictionary_0.Count;
            int num = this.queue_0.Count;
            return string.Format("{0} waiting exec, {1} subscriptions, {2} threads: {3} in use, {4} idle", new object[] { num, count, this.int_0, this.int_2, this.int_1 });
        }

        public int IdleThreads
        {
            get
            {
                return this.int_1;
            }
        }

        public int InUseThreads
        {
            get
            {
                return this.int_2;
            }
        }

        public int Threads
        {
            get
            {
                return this.int_0;
            }
        }

        public int Treshold
        {
            get
            {
                return this.queue_0.Count;
            }
        }
    }
}

