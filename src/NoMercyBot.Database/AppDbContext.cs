using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercyBot.Database.Models;
using NoMercyBot.Database.Models.ChatMessage;
using NoMercyBot.Globals.Information;
using Stream = NoMercyBot.Database.Models.Stream;

namespace NoMercyBot.Database;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
        //
    }

    public AppDbContext()
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured) return;

        optionsBuilder.UseSqlite($"Data Source={AppFiles.DatabaseFile}; Pooling=True",
            o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder.Properties<string>()
            .HaveMaxLength(256);

        configurationBuilder
            .Properties<Ulid>()
            .HaveConversion<UlidToStringConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Model.GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => p.ClrType == typeof(Ulid))
            .ToList()
            .ForEach(p => p.SetElementType(typeof(string)));

        modelBuilder.Model.GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => p.Name is "CreatedAt" or "UpdatedAt")
            .ToList()
            .ForEach(p => p.SetDefaultValueSql("CURRENT_TIMESTAMP"));

        modelBuilder.Model.GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => p.ClrType == typeof(DateTime))
            .ToList()
            .ForEach(p => p.SetDefaultValue(null));

        modelBuilder.Model.GetEntityTypes()
            .SelectMany(t => t.GetForeignKeys())
            .ToList()
            .ForEach(p => p.DeleteBehavior = DeleteBehavior.Cascade);

        // Make sure to encrypt and decrypt the access and refresh tokens
        modelBuilder.Entity<Service>()
            .Property(e => e.AccessToken)
            .HasConversion(
                v => TokenStore.EncryptToken(v),
                v => TokenStore.DecryptToken(v));

        modelBuilder.Entity<Service>()
            .Property(e => e.RefreshToken)
            .HasConversion(
                v => TokenStore.EncryptToken(v),
                v => TokenStore.DecryptToken(v));

        modelBuilder.Entity<Service>()
            .Property(e => e.ClientId)
            .HasConversion(
                v => TokenStore.EncryptToken(v),
                v => TokenStore.DecryptToken(v));

        modelBuilder.Entity<Service>()
            .Property(e => e.ClientSecret)
            .HasConversion(
                v => TokenStore.EncryptToken(v),
                v => TokenStore.DecryptToken(v));

        modelBuilder.Entity<ChatMessage>()
            .HasOne(m => m.ReplyToMessage)
            .WithMany(m => m.Replies)
            .HasForeignKey(m => m.ReplyToMessageId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ChatMessage>()
            .Property(e => e.Badges)
            .HasConversion(
                v => JsonConvert.SerializeObject(v),
                v => JsonConvert.DeserializeObject<List<ChatBadge>>(v) ?? new List<ChatBadge>());

        modelBuilder.Entity<ChatMessage>()
            .Property(e => e.Fragments)
            .HasConversion(
                v => JsonConvert.SerializeObject(v),
                v => JsonConvert.DeserializeObject<List<ChatMessageFragment>>(v) ?? new List<ChatMessageFragment>());

        modelBuilder.Entity<ChatEmote>()
            .Property(e => e.Urls)
            .HasConversion(
                v => JsonConvert.SerializeObject(v),
                v => JsonConvert.DeserializeObject<Dictionary<string, Uri>>(v) ?? new Dictionary<string, Uri>());

        modelBuilder.Entity<ChannelInfo>()
            .Property(e => e.Tags)
            .HasConversion(
                v => JsonConvert.SerializeObject(v),
                v => JsonConvert.DeserializeObject<List<string>>(v) ?? new List<string>());

        modelBuilder.Entity<ChannelInfo>()
            .Property(e => e.ContentLabels)
            .HasConversion(
                v => JsonConvert.SerializeObject(v),
                v => JsonConvert.DeserializeObject<List<string>>(v) ?? new List<string>());

        modelBuilder.Entity<EventSubscription>()
            .Property(e => e.Metadata)
            .HasConversion(
                v => JsonConvert.SerializeObject(v),
                v => JsonConvert.DeserializeObject<Dictionary<string, string>>(v) ?? new Dictionary<string, string>());

        modelBuilder.Entity<EventSubscription>()
            .Property(e => e.Condition)
            .HasConversion(
                v => JsonConvert.SerializeObject(v),
                v => JsonConvert.DeserializeObject<string[]>(v) ?? Array.Empty<string>());

        modelBuilder.Entity<User>()
            .Property(e => e.Pronoun)
            .HasConversion(
                v => JsonConvert.SerializeObject(v),
                v => JsonConvert.DeserializeObject<Pronoun>(v) ?? new Pronoun());

        // Configure the ChatPresence-User (as Channel) relationship
        modelBuilder.Entity<ChatPresence>()
            .HasOne(cp => cp.Channel)
            .WithMany()
            .HasForeignKey(cp => cp.ChannelId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure the ChatPresence-User relationship
        modelBuilder.Entity<ChatPresence>()
            .HasOne(cp => cp.User)
            .WithMany()
            .HasForeignKey(cp => cp.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure the Channel-ChatPresence relationship
        modelBuilder.Entity<Channel>()
            .HasMany(c => c.UsersInChat)
            .WithOne()
            .HasForeignKey(cp => cp.ChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BotAccount>()
            .Property(e => e.ClientId)
            .HasConversion(
                v => TokenStore.EncryptToken(v),
                v => TokenStore.DecryptToken(v));

        modelBuilder.Entity<BotAccount>()
            .Property(e => e.ClientSecret)
            .HasConversion(
                v => TokenStore.EncryptToken(v),
                v => TokenStore.DecryptToken(v));

        modelBuilder.Entity<BotAccount>()
            .Property(e => e.AccessToken)
            .HasConversion(
                v => TokenStore.EncryptToken(v),
                v => TokenStore.DecryptToken(v));

        modelBuilder.Entity<BotAccount>()
            .Property(e => e.RefreshToken)
            .HasConversion(
                v => TokenStore.EncryptToken(v),
                v => TokenStore.DecryptToken(v));

        modelBuilder.Entity<BotAccount>()
            .Property(e => e.TokenExpiry)
            .IsRequired(false);

        modelBuilder.Entity<Configuration>()
            .Property(e => e.SecureValue)
            .HasConversion(
                v => TokenStore.EncryptToken(v),
                v => TokenStore.DecryptToken(v));

        modelBuilder.Entity<Storage>()
            .Property(e => e.SecureValue)
            .HasConversion(
                v => TokenStore.EncryptToken(v),
                v => TokenStore.DecryptToken(v));

        modelBuilder.Entity<ChannelEvent>()
            .Property(e => e.Data)
            .HasConversion(
                v => JsonConvert.SerializeObject(v),
                v => JsonConvert.DeserializeObject(v));

        base.OnModelCreating(modelBuilder);
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Channel> Channels { get; set; }
    public DbSet<ChannelInfo> ChannelInfo { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<ChatPresence> ChatPresences { get; set; }
    public DbSet<Configuration> Configurations { get; set; }
    public DbSet<Service> Services { get; set; }
    public DbSet<Pronoun> Pronouns { get; set; }
    public DbSet<EventSubscription> EventSubscriptions { get; set; }
    public DbSet<BotAccount> BotAccounts { get; set; }
    public DbSet<Stream> Streams { get; set; }
    public DbSet<Widget> Widgets { get; set; }
    public DbSet<ChannelEvent> ChannelEvents { get; set; }
    public DbSet<TtsVoice> TtsVoices { get; set; }
    public DbSet<UserTtsVoice> UserTtsVoices { get; set; }
    public DbSet<TtsProvider> TtsProviders { get; set; }
    public DbSet<TtsUsageRecord> TtsUsageRecords { get; set; }
    public DbSet<TtsCacheEntry> TtsCacheEntries { get; set; }
    public DbSet<Command> Commands { get; set; }
    public DbSet<Reward> Rewards { get; set; }
    public DbSet<Storage> Storages { get; set; }
    public DbSet<Record> Records { get; set; }
}