using Doosii.DAL.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Doosii.DAL.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20261013160000_RenameUserRoles")]
    public class RenameUserRoles : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE [auth].[Users] SET [Role] = 'CasualUser' WHERE [Role] = 'Customer';");
            migrationBuilder.Sql("UPDATE [auth].[Users] SET [Role] = 'Shopper' WHERE [Role] = 'Seller';");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE [auth].[Users] SET [Role] = 'Customer' WHERE [Role] = 'CasualUser';");
            migrationBuilder.Sql("UPDATE [auth].[Users] SET [Role] = 'Seller' WHERE [Role] = 'Shopper';");
        }
    }
}
