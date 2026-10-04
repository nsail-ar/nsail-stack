// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.BaseServices.WebApi;
using NSail.Metadata;

namespace NSail.BaseServices.WebApi.Tests;

// The document-level proof lives in the Architecture suite (SwaggerDocumentTests asks every
// host for its OpenAPI document). What is here is the shape of the id itself, on the three
// cases a derived name can get wrong: the homonym it exists for, a generic type whose
// arguments the name has to carry, and a nested type whose declaring type the namespace
// convention cannot see.
public sealed class SchemaIdResolverTests
{
    readonly SchemaIdResolver _resolver = new(new MetadataProvider());

    [Fact]
    public void TheAreaQualifiesTheName()
    {
        Assert.Equal(
            "Optical.Prescriptions.CreatePrescriptionBody",
            _resolver.For(typeof(Optical.Prescriptions.Models.CreatePrescriptionBody)));

        Assert.Equal(
            "Medical.Prescriptions.CreatePrescriptionBody",
            _resolver.For(typeof(Medical.Prescriptions.Models.CreatePrescriptionBody)));
    }

    [Fact]
    public void AGenericTypeCarriesItsArguments()
    {
        Assert.Equal(
            "Types.PageOfMedical.Prescriptions.Line",
            _resolver.For(typeof(Types.Page<Medical.Prescriptions.Models.Line>)));

        Assert.Equal(
            "Types.PairOfTypes.PageOfInt32AndMedical.Prescriptions.Line",
            _resolver.For(typeof(Types.Pair<Types.Page<int>, Medical.Prescriptions.Models.Line>)));
    }

    [Fact]
    public void ANestedTypeCarriesItsDeclaringType()
    {
        Assert.Equal(
            "Medical.Prescriptions.CreatePrescriptionBody.Line",
            _resolver.For(typeof(Medical.Prescriptions.Models.CreatePrescriptionBody.Line)));

        Assert.NotEqual(
            _resolver.For(typeof(Medical.Prescriptions.Models.Line)),
            _resolver.For(typeof(Medical.Prescriptions.Models.CreatePrescriptionBody.Line)));
    }
}
