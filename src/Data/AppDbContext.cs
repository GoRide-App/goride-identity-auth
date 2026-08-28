using Microsoft.EntityFrameworkCore;

namespace SRC.Data;

public class AppDbContext: DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options): base(options){}

    
}