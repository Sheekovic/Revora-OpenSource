// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

internal abstract class Class2 : IDisposable
{
    internal bool bool_0;
    internal bool bool_1;
    internal static volatile int int_0 = -2147483648;
    protected int int_1;
    internal Time32 time32_0;

    public Class2()
    {
        int_0++;
        this.int_1 = int_0;
        this.bool_0 = true;
        this.bool_1 = false;
        this.method_1(0);
    }

    ~Class2()
    {
        ((IDisposable)this).Dispose();
    }

    public override int GetHashCode()
    {
        return this.int_1;
    }

    internal bool method_0()
    {
        return (Time32.Now > this.time32_0);
    }

    internal void method_1(int int_2)
    {
        this.time32_0 = Time32.Now.AddMilliseconds(int_2);
    }

    void IDisposable.Dispose()
    {
        this.bool_0 = false;
        this.vmethod_1();
    }

    internal abstract void vmethod_0();
    internal abstract void vmethod_1();
    internal abstract MethodInfo vmethod_2();
    internal abstract ThreadPriority vmethod_3();
}