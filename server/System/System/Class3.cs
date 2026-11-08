// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Threading;
using System.Threading.Generic;

internal sealed class Class3<T> : Class2
{
    private T gparam_0;
    private TimerRule<T> timerRule_0;

    public Class3(TimerRule<T> timerRule_1, T gparam_1)
    {
        this.timerRule_0 = timerRule_1;
        this.gparam_0 = gparam_1;
    }

    internal override void vmethod_0()
    {
        if (this.timerRule_0 != null)
        {
            this.timerRule_0.action_0(this.gparam_0, Time32.Now.Value);
            if (this.timerRule_0 != null)
            {
                if (!this.timerRule_0.bool_0)
                {
                    ((IDisposable)this).Dispose();
                }
                else
                {
                    base.method_1(this.timerRule_0.int_0);
                }
            }
        }
    }

    internal override void vmethod_1()
    {
        this.timerRule_0 = null;
        this.gparam_0 = default(T);
    }

    internal override MethodInfo vmethod_2()
    {
        return this.timerRule_0.action_0.Method;
    }

    internal override ThreadPriority vmethod_3()
    {
        return this.timerRule_0.threadPriority_0;
    }
}