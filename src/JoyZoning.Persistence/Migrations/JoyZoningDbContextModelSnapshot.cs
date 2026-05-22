using System;
using JoyZoning.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace JoyZoning.Persistence.Migrations;

[DbContext(typeof(JoyZoningDbContext))]
partial class JoyZoningDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "6.0.29");
        // EnsureCreated uses OnModelCreating; migrations are for explicit upgrade path.
    }
}
