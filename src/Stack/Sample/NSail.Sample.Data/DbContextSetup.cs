// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using NSail.Data;
using NSail.Sample.Entities;

namespace NSail.Sample;

public class DbContextSetup : IDbContextSetup
{
    public void Configure(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<Contact>();

        builder.ToTable("Contacts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Email).HasMaxLength(200);
        builder.Property(x => x.Phone).HasMaxLength(50);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.HasIndex(x => x.Name);
    }
}
