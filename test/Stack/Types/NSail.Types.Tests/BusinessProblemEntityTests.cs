// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Types.Tests;

// The entity a refusal is about crosses the wire as a KEY, because the reader is a browser
// that carries the catalogs and not the assembly the type lives in. What stays a bare name is
// what a reader with no catalog is left with: the English message and Issue.Source, which is
// also what "Problems.{Code}.{Source}" is scoped by.
public sealed class BusinessProblemEntityTests
{
    sealed class Store
    {
    }

    [Fact]
    public void NotFound_carries_the_concept_key_and_names_the_type()
    {
        var issue = Assert.Single(BusinessProblem.NotFound(typeof(Store), 7).Issues);

        Assert.NotNull(issue.Arguments);
        Assert.Equal("Types.Store", issue.Arguments["entity"]);
        Assert.Equal("7", issue.Arguments["id"]);
        Assert.Equal("Store", issue.Source);
        Assert.Equal("Store with id 7 was not found", issue.Message);
    }

    [Fact]
    public void AlreadyExists_carries_the_concept_key_and_names_the_type()
    {
        var issue = Assert.Single(BusinessProblem.AlreadyExists(typeof(Store), "Central").Issues);

        Assert.NotNull(issue.Arguments);
        Assert.Equal("Types.Store", issue.Arguments["entity"]);
        Assert.Equal("Store", issue.Source);
        Assert.Equal("Store with value Central already exists", issue.Message);
    }

    [Fact]
    public void InUse_carries_the_concept_key_and_names_the_type()
    {
        var issue = Assert.Single(BusinessProblem.InUse(typeof(Store)).Issues);

        Assert.NotNull(issue.Arguments);
        Assert.Equal("Types.Store", issue.Arguments["entity"]);
        Assert.Equal("Store", issue.Source);
    }

    [Fact]
    public void Conflict_carries_the_concept_key_and_names_the_type()
    {
        var issue = Assert.Single(BusinessProblem.Conflict(typeof(Store)).Issues);

        Assert.NotNull(issue.Arguments);
        Assert.Equal("Types.Store", issue.Arguments["entity"]);
        Assert.Equal("Store", issue.Source);
        Assert.Equal("The resource Store was modified by another process", issue.Message);
    }

    // The caller with no type in scope writes the key itself, and everything a reader without a
    // catalog sees is still the short name.
    [Fact]
    public void A_written_key_names_its_last_segment()
    {
        var issue = Assert.Single(BusinessProblem.NotFound("Products.Product", 3).Issues);

        Assert.NotNull(issue.Arguments);
        Assert.Equal("Products.Product", issue.Arguments["entity"]);
        Assert.Equal("Product", issue.Source);
        Assert.Equal("Product with id 3 was not found", issue.Message);
    }

    [Fact]
    public void An_explicit_source_still_wins()
    {
        var issue = Assert.Single(BusinessProblem.NotFound(typeof(Store), 7, "StoreId").Issues);

        Assert.Equal("StoreId", issue.Source);
        Assert.Equal("Types.Store", issue.Arguments!["entity"]);
    }
}
