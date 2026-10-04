// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;

namespace NSail.Configuration.Tests;

public class ConfigurationExtensionsTests
{
    [Fact]
    public void Load_BindsSectionAndValidatesRequiredProperties()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Iam:EncryptionKey"] = "secret"
        });

        var settings = configuration.Load<IamSettings>("Iam");

        Assert.Equal("secret", settings.EncryptionKey);
    }

    [Fact]
    public void Load_ThrowsWhenRequiredPropertyIsMissing()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Iam:EncryptionKey"] = ""
        });

        var exception = Assert.Throws<InvalidOperationException>(() => configuration.Load<IamSettings>("Iam"));

        Assert.Contains("EncryptionKey", exception.Message);
        Assert.Contains("Configuration validation failed", exception.Message);
    }

    [Fact]
    public void Load_DerivesSectionFromTypeNameMinusOptionsSuffix()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Sample:ConnectionString"] = "Server=.;Database=Sample"
        });

        var settings = configuration.Load<SampleOptions>();

        Assert.Equal("Server=.;Database=Sample", settings.ConnectionString);
    }

    [Fact]
    public void Load_UsesFullTypeNameWhenItHasNoOptionsSuffix()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ApiSettings:ApiKey"] = "key-123"
        });

        var settings = configuration.Load<ApiSettings>();

        Assert.Equal("key-123", settings.ApiKey);
    }

    [Fact]
    public void Load_ValidatesOpticalConnectionString()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Optical:ConnectionString"] = "Server=.;Database=Optical"
        });

        var settings = configuration.Load<OpticalSettings>("Optical");

        Assert.Equal("Server=.;Database=Optical", settings.ConnectionString);
    }

    [Fact]
    public void Load_ThrowsWhenOpticalConnectionStringIsMissing()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Optical:ConnectionString"] = ""
        });

        var exception = Assert.Throws<InvalidOperationException>(() => configuration.Load<OpticalSettings>("Optical"));

        Assert.Contains("ConnectionString", exception.Message);
    }

    static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    sealed class IamSettings
    {
        [Required]
        public string EncryptionKey { get; set; } = "";
    }

    sealed class ApiSettings
    {
        [Required]
        public string ApiKey { get; set; } = "";
    }

    sealed class OpticalSettings
    {
        [Required]
        public string ConnectionString { get; set; } = "";
    }

    sealed class SampleOptions
    {
        [Required]
        public string ConnectionString { get; set; } = "";
    }
}
