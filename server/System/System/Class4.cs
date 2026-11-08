// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Threading;

internal sealed class Class4 : Class2
{
    private TimerRule timerRule_0;

    public Class4(TimerRule timerRule_1)
    {
        this.timerRule_0 = timerRule_1;
    }

    internal override void vmethod_0()
    {
        if (this.timerRule_0 != null)
        {
            this.timerRule_0.action_0(Time32.Now.Value);
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