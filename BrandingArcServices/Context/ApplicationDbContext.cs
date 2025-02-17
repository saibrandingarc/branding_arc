using System.Xml;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using BrandingArcServices.Models;

namespace BrandingArcServices.Context
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Define DbSets for your entities
        public DbSet<Settings> Settings { get; set; }
        public DbSet<ZohoCasesMonth> ZohoCasesMonths { get; set; }
        public DbSet<ZohoDeliverablesMonth> ZohoDeliverablesMonths { get; set; }
        public DbSet<ZohoFutureDeliverables> ZohoFutureDeliverabless { get; set; }
        public DbSet<ClientDetail> ClientDetails { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<User>().ToTable("Users");
            // Configure entity properties and relationships here
        }
    }
}
