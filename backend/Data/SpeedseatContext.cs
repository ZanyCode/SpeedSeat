using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

public class SpeedseatContext: DbContext 
{        
    public SpeedseatContext(DbContextOptions options) : base(options)
    {
        this.Database.EnsureCreated();
    }

    public DbSet<Setting> Settings { get; set; }

    public string? Get(string id) {
        return this.Settings.Find(id)?.Value;
    }

    public void Set(string id, string value) {
        var setting = this.Settings.Find(id);
        if(setting != null) {
            setting.Value = value.ToString();
            this.Update(setting);
            this.SaveChanges();
        }
        else {
            this.Add(new Setting { Id = id, Value = value });
            this.SaveChanges();
        }
    } 
}

public class Setting{
    [Key]
    public string Id { get; set; }

    public string Value { get; set; }
}