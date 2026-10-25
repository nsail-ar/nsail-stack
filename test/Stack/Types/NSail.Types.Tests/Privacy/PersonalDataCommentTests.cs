// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Privacy;

namespace NSail.Types.Tests.Privacy;

// One spelling shared by the model pass that writes a column's comment and whatever reads the
// catalog back: the round trip is the whole contract, and the words are what outlive this
// assembly in databases nobody will migrate for the sake of a rename.
public sealed class PersonalDataCommentTests
{
    [Fact]
    public void Every_kind_has_a_word_and_reads_back_as_itself()
    {
        foreach (var kind in Enum.GetValues<PersonalData>())
        {
            var comment = PersonalDataComment.For(kind);

            Assert.StartsWith(PersonalDataComment.Prefix, comment, StringComparison.Ordinal);
            Assert.Equal(kind, PersonalDataComment.In(comment));
        }
    }

    [Fact]
    public void The_words_are_the_ones_already_in_the_catalog()
    {
        Assert.Equal("personal:name", PersonalDataComment.For(PersonalData.Name));
        Assert.Equal("personal:document", PersonalDataComment.For(PersonalData.Document));
        Assert.Equal("personal:email", PersonalDataComment.For(PersonalData.Email));
        Assert.Equal("personal:phone", PersonalDataComment.For(PersonalData.Phone));
        Assert.Equal("personal:address", PersonalDataComment.For(PersonalData.Address));
        Assert.Equal("personal:free-text", PersonalDataComment.For(PersonalData.FreeText));
        Assert.Equal("personal:birth-date", PersonalDataComment.For(PersonalData.BirthDate));
    }

    [Fact]
    public void A_kind_with_no_word_is_refused_where_it_is_written()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PersonalDataComment.For((PersonalData)999));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("The customer's own name")]
    [InlineData("personal")]
    [InlineData("not-personal:name")]
    public void Anything_that_is_not_the_declaration_declares_nothing(string? comment)
    {
        Assert.Null(PersonalDataComment.In(comment));
    }

    // A word a newer Stack writes reaching an older reader: no declaration, not a throw — a copy
    // of a tenant must not fall over on a column it does not recognize.
    [Fact]
    public void A_word_this_version_does_not_know_reads_as_no_declaration()
    {
        Assert.Null(PersonalDataComment.In("personal:biometric"));
    }

    [Fact]
    public void The_declaration_is_read_however_it_was_cased_or_padded()
    {
        Assert.Equal(PersonalData.Email, PersonalDataComment.In("  PERSONAL:Email  "));
    }
}
