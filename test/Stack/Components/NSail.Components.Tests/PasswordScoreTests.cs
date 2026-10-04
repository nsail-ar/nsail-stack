// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components.Tests;

/// <summary>The buckets NsPasswordField's strength bar draws (nsail#501). Pinned here rather
/// than off a rendered bar: the score is the decision, the bar is only how it is shown, and
/// advice that silently drifts one bucket is advice nobody can rely on. It is advice — no
/// bucket refuses a submit anywhere.</summary>
public sealed class PasswordScoreTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Nothing_typed_earns_no_verdict(string? password)
    {
        Assert.Null(PasswordScore.Measure(password));
    }

    [Theory]
    [InlineData("a")]
    [InlineData("Ab1!")]
    [InlineData("Abc123")]
    public void Too_short_is_weak_however_varied(string password)
    {
        Assert.Equal(PasswordStrength.Weak, PasswordScore.Measure(password));
    }

    // Length alone buys nothing: one alphabet is one alphabet however much of it is typed.
    [Theory]
    [InlineData("aaaaaaaaaaaaaaaaaaaa")]
    [InlineData("contrasenalarguisima")]
    [InlineData("12345678901234567890")]
    public void One_character_class_is_weak_however_long(string password)
    {
        Assert.Equal(PasswordStrength.Weak, PasswordScore.Measure(password));
    }

    [Theory]
    [InlineData("password1")]
    [InlineData("Password1")]
    [InlineData("contrasena12")]
    public void Long_enough_with_some_variety_is_medium(string password)
    {
        Assert.Equal(PasswordStrength.Medium, PasswordScore.Measure(password));
    }

    [Theory]
    [InlineData("Password1234")]
    [InlineData("mi-Contrasena-2026")]
    [InlineData("Tr0ub4dor&3xyz")]
    public void Long_and_varied_is_strong(string password)
    {
        Assert.Equal(PasswordStrength.Strong, PasswordScore.Measure(password));
    }

    // The two edges the buckets turn on, so a change to either constant fails here rather
    // than quietly regrading every password in the install.
    [Fact]
    public void The_bucket_edges_are_eight_and_twelve_characters()
    {
        Assert.Equal(PasswordStrength.Weak, PasswordScore.Measure("Passwor1"[..7]));
        Assert.Equal(PasswordStrength.Medium, PasswordScore.Measure("Passwor1"));

        Assert.Equal(PasswordStrength.Medium, PasswordScore.Measure("Password12!"));
        Assert.Equal(PasswordStrength.Strong, PasswordScore.Measure("Password123!"));
    }
}
