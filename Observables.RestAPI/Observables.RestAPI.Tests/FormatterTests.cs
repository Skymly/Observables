using System.Runtime.Serialization;

namespace Observables.RestAPI.Tests;

public sealed class FormatterTests
{
    [Flags]
    public enum Perm
    {
        None = 0,
        [EnumMember(Value = "read")]
        Read = 1,
        Write = 2,
        Exec = 4,
    }

    [Fact]
    public void Url_formatter_Flags_combination_does_not_throw()
    {
        var formatter = new DefaultUrlParameterFormatter();
        var result = formatter.Format(Perm.Read | Perm.Write, typeof(object), typeof(Perm));
        Assert.Equal("Read, Write", result);
    }

    [Fact]
    public void Url_formatter_undefined_enum_value_does_not_throw()
    {
        var formatter = new DefaultUrlParameterFormatter();
        var result = formatter.Format((Perm)32, typeof(object), typeof(Perm));
        Assert.Equal("32", result);
    }

    [Fact]
    public void Url_formatter_uses_EnumMember_value()
    {
        var formatter = new DefaultUrlParameterFormatter();
        var result = formatter.Format(Perm.Read, typeof(object), typeof(Perm));
        Assert.Equal("read", result);
    }

    [Fact]
    public void Form_formatter_Flags_combination_does_not_throw()
    {
        var formatter = new DefaultFormUrlEncodedParameterFormatter();
        var result = formatter.Format(Perm.Read | Perm.Exec, null);
        Assert.Equal("Read, Exec", result);
    }

    [Fact]
    public void Form_formatter_undefined_enum_value_does_not_throw()
    {
        var formatter = new DefaultFormUrlEncodedParameterFormatter();
        var result = formatter.Format((Perm)16, null);
        Assert.Equal("16", result);
    }
}
