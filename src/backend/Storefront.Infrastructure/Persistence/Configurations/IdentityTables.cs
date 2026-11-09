using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Storefront.Infrastructure.Identity;

namespace Storefront.Infrastructure.Persistence.Configurations;

/// <summary>Table names for the ASP.NET Identity tables and the data protection keys (columns become snake_case in the context).</summary>
internal static class IdentityTables
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AdminUser>().ToTable("admin_users");
        modelBuilder.Entity<IdentityRole<Guid>>().ToTable("admin_roles");
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("admin_user_roles");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("admin_user_claims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("admin_user_logins");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("admin_user_tokens");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("admin_role_claims");
        modelBuilder.Entity<DataProtectionKey>().ToTable("data_protection_keys");
    }
}
