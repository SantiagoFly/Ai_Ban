using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Backend.Models;

namespace Backend.DataAccess
{

    /// <summary>
    /// SQL Data context   
    /// </summary>
    public class DatabaseContext : DbContext
    {
        private readonly IConfiguration config;

        public DbSet<User> Users { get; set; }

        public DbSet<RequestHistory> RequestsHistory { get; set; }

        public DbSet<StartUpOption> StartUpOptions { get; set; }

        public DbSet<Conversation> Conversations { get; }

        public DbSet<LibraryStructure> LibraryStructure { get; }

        public DbSet<AiPlugin> AiPlugins { get; }
        

        /// <summary>
        /// EF Core constructor
        /// </summary>
        public DatabaseContext()
        {
        }


        /// <summary>
        /// EF Core constructor
        /// </summary>
        public DatabaseContext(DbContextOptions options) : base(options)
        {

        }


        /// <summary>
        /// Contrustor used when a configuration is passed
        /// </summary>
        public DatabaseContext(IConfiguration config)
        {
            this.config = config;
        }


        /// <summary>
        /// Configures the data context
        /// </summary>
        /// <param name="optionsBuilder"></param>
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (optionsBuilder == null) return;
            base.OnConfiguring(optionsBuilder);

            var connectionString = string.Empty;
            var databaseName = string.Empty;
            if (this.config != null)
            {
                databaseName = config["DatabaseName"].ToString();
                connectionString = config["DatabaseConnectionString"]?.ToString();
            }

            if (!string.IsNullOrEmpty(connectionString))
            {
#if DEBUG
                optionsBuilder.EnableSensitiveDataLogging(true);
#endif
                optionsBuilder.UseCosmos(connectionString, databaseName);
            }
        }


        /// <summary>
        /// Seeds the BD
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>().ToContainer("users");
            modelBuilder.Entity<User>().HasNoDiscriminator();
            modelBuilder.Entity<User>().HasKey(x => x.UserId);
            modelBuilder.Entity<User>().HasPartitionKey(x => x.UserId);       

            modelBuilder.Entity<RequestHistory>().ToContainer("request-history");
            modelBuilder.Entity<RequestHistory>().HasNoDiscriminator();
            modelBuilder.Entity<RequestHistory>().HasKey(x => x.RequestId);
            modelBuilder.Entity<RequestHistory>().HasPartitionKey(x => x.UserId);
        
            modelBuilder.Entity<Conversation>().ToContainer("conversations");
            modelBuilder.Entity<Conversation>().HasNoDiscriminator();
            modelBuilder.Entity<Conversation>().HasKey(x => x.ConversationId);
            modelBuilder.Entity<Conversation>().HasPartitionKey(x => x.UserId);
            modelBuilder.Entity<Conversation>().OwnsMany(rh => rh.History);

            modelBuilder.Entity<StartUpOption>().ToContainer("startup-options");
            modelBuilder.Entity<StartUpOption>().HasNoDiscriminator();
            modelBuilder.Entity<StartUpOption>().HasKey(x => x.StartUpOptionId);
            modelBuilder.Entity<StartUpOption>().HasPartitionKey(x => x.StartUpOptionId);

            modelBuilder.Entity<LibraryStructure>().ToContainer("library-structure");
            modelBuilder.Entity<LibraryStructure>().HasNoDiscriminator();
            modelBuilder.Entity<LibraryStructure>().HasKey(x => x.Id);
            modelBuilder.Entity<LibraryStructure>().HasPartitionKey(x => x.Id);
            modelBuilder.Entity<LibraryStructure>().OwnsMany(x => x.Documents);

            modelBuilder.Entity<AiPlugin>().ToContainer("ai-plugins");
            modelBuilder.Entity<AiPlugin>().HasNoDiscriminator();
            modelBuilder.Entity<AiPlugin>().HasKey(x => x.Id);
            modelBuilder.Entity<AiPlugin>().HasPartitionKey(x => x.Id);        
            
        }
    }
}
