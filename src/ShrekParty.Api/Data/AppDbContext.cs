using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ShrekParty.Api.Auction;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Days;
using ShrekParty.Api.HideAndSeek;
using ShrekParty.Api.Laugh;
using ShrekParty.Api.Neuro;
using ShrekParty.Api.PhotoHunt;
using ShrekParty.Api.Quiz;
using ShrekParty.Api.Reaction;
using ShrekParty.Api.Tapper;
using ShrekParty.Api.Tote;
using ShrekParty.Api.Tournament;
using ShrekParty.Api.Tv;

namespace ShrekParty.Api.Data;

/// <summary>
/// База приложения (SQLite).
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Event> Events => Set<Event>();

    public DbSet<DonkeyAward> DonkeyAwards => Set<DonkeyAward>();

    public DbSet<MediaFile> MediaFiles => Set<MediaFile>();

    public DbSet<RegistrationAttempt> RegistrationAttempts => Set<RegistrationAttempt>();


    public DbSet<TournamentMatch> TournamentMatches => Set<TournamentMatch>();

    public DbSet<Bet> Bets => Set<Bet>();

    public DbSet<LaughScore> LaughScores => Set<LaughScore>();

    public DbSet<HideShot> HideShots => Set<HideShot>();

    public DbSet<HideShotPlayer> HideShotPlayers => Set<HideShotPlayer>();

    public DbSet<HideVote> HideVotes => Set<HideVote>();

    public DbSet<PhotoSubmission> PhotoSubmissions => Set<PhotoSubmission>();

    public DbSet<PhotoLike> PhotoLikes => Set<PhotoLike>();

    public DbSet<TvMessage> TvMessages => Set<TvMessage>();

    public DbSet<CaptionTemplate> CaptionTemplates => Set<CaptionTemplate>();

    public DbSet<ReactionAttempt> ReactionAttempts => Set<ReactionAttempt>();

    public DbSet<TapScore> TapScores => Set<TapScore>();

    public DbSet<DaysAnswer> DaysAnswers => Set<DaysAnswer>();

    public DbSet<NeuroPhoto> NeuroPhotos => Set<NeuroPhoto>();

    public DbSet<NeuroGuess> NeuroGuesses => Set<NeuroGuess>();

    public DbSet<QuizTheme> QuizThemes => Set<QuizTheme>();

    public DbSet<QuizQuestion> QuizQuestions => Set<QuizQuestion>();

    public DbSet<QuizPress> QuizPresses => Set<QuizPress>();

    public DbSet<QuizScoreEntry> QuizScoreEntries => Set<QuizScoreEntry>();

    public DbSet<QuizFinalAnswer> QuizFinalAnswers => Set<QuizFinalAnswer>();

    public DbSet<AuctionLot> AuctionLots => Set<AuctionLot>();

    public DbSet<AuctionSale> AuctionSales => Set<AuctionSale>();

    public DbSet<Pet> Pets => Set<Pet>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasIndex(u => u.LoginNormalized).IsUnique();
        modelBuilder.Entity<Pet>().HasKey(p => p.UserId);
        modelBuilder.Entity<RegistrationAttempt>().HasIndex(a => a.LoginNormalized).IsUnique();
        modelBuilder.Entity<DonkeyAward>().HasIndex(a => a.UserId);
        modelBuilder.Entity<TournamentMatch>().HasIndex(m => new { m.EventId, m.Index }).IsUnique();
        modelBuilder.Entity<Bet>().HasIndex(b => new { b.MatchId, b.UserId }).IsUnique();
        modelBuilder.Entity<LaughScore>().HasIndex(s => new { s.EventId, s.UserId, s.Game }).IsUnique();
        modelBuilder.Entity<HideShotPlayer>().HasIndex(p => new { p.ShotId, p.UserId }).IsUnique();
        modelBuilder.Entity<HideVote>().HasIndex(v => new { v.EventId, v.VoterId }).IsUnique();
        modelBuilder.Entity<PhotoSubmission>().HasIndex(s => new { s.EventId, s.UserId }).IsUnique();
        modelBuilder.Entity<PhotoLike>().HasIndex(l => new { l.EventId, l.OwnerId, l.LikerId }).IsUnique();
        modelBuilder.Entity<ReactionAttempt>().HasIndex(a => new { a.EventId, a.UserId, a.Round }).IsUnique();
        modelBuilder.Entity<TapScore>().HasIndex(t => new { t.EventId, t.UserId }).IsUnique();
        modelBuilder.Entity<DaysAnswer>().HasIndex(a => new { a.EventId, a.UserId }).IsUnique();
        modelBuilder.Entity<NeuroGuess>().HasIndex(g => new { g.EventId, g.PhotoId, g.UserId }).IsUnique();
        modelBuilder.Entity<QuizPress>().HasIndex(p => new { p.EventId, p.QuestionId, p.UserId }).IsUnique();
        modelBuilder.Entity<QuizFinalAnswer>().HasIndex(f => new { f.EventId, f.UserId }).IsUnique();
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite не умеет сортировать DateTimeOffset — храним числом (всё время у нас UTC).
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
        configurationBuilder.Properties<Enum>().HaveConversion<string>();
    }
}
