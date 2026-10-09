using SyWater.Places.Application.Geography;
using SyWater.Places.Application.Places;
using SyWater.Places.Application.Ports.Out;
using SyWater.Places.Application.Tips;
using SyWater.Places.Application.UseCases;
using SyWater.Places.Domain.Places;
using SyWater.Places.Domain.Tips;

namespace SyWater.Places.Application.Tests;

/// <summary>In-memory adapter of the tips and the favorites of every user.</summary>
internal sealed class InMemoryTipRepository : ITipRepository, ITipFavoriteRepository
{
    public List<Tip> Tips { get; } = [];
    public List<(Guid UserId, Guid TipId, int Order)> Favorites { get; } = [];
    private int _order;

    public Task<Tip?> GetAsync(Guid tipId, CancellationToken ct) => Task.FromResult(Tips.FirstOrDefault(t => t.Id == tipId));

    public Task<IReadOnlyList<Tip>> ListAsync(TipCategory? category, bool includeInactive, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Tip>>(Tips
            .Where(t => (category is null || t.Category == category) && (includeInactive || t.IsActive))
            .OrderByDescending(t => t.CreatedAt).ToList());

    public Task<IReadOnlyList<Tip>> ListActiveAsync(TipCategory category, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Tip>>(Tips.Where(t => t.Category == category && t.IsActive).ToList());

    public Task AddAsync(Tip tip, CancellationToken ct)
    {
        Tips.Add(tip);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Tip tip, CancellationToken ct) => Task.CompletedTask;   // same instance in memory

    public Task<IReadOnlySet<Guid>> ListIdsAsync(Guid userId, CancellationToken ct) =>
        Task.FromResult<IReadOnlySet<Guid>>(Favorites.Where(f => f.UserId == userId).Select(f => f.TipId).ToHashSet());

    public Task AddAsync(Guid userId, Guid tipId, CancellationToken ct)
    {
        if (!Favorites.Any(f => f.UserId == userId && f.TipId == tipId)) Favorites.Add((userId, tipId, ++_order));
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Guid userId, Guid tipId, CancellationToken ct)
    {
        Favorites.RemoveAll(f => f.UserId == userId && f.TipId == tipId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Tip>> ListActiveTipsAsync(Guid userId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Tip>>(Favorites.Where(f => f.UserId == userId).OrderByDescending(f => f.Order)
            .Select(f => Tips.First(t => t.Id == f.TipId)).Where(t => t.IsActive).ToList());
}

/// <summary>HU-063 (admin creates), HU-064 (edit / deactivate) and HU-065 (the user sees tips and favorites).</summary>
public class TipTests
{
    private readonly InMemoryTipRepository _tips = new();
    private readonly InMemoryPlaceRepository _places = new();
    private readonly FakeGeographyReader _geography = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 10, 9, 15, 0, 0, TimeSpan.Zero));
    private readonly Guid _admin = Guid.NewGuid();
    private readonly Guid _user = Guid.NewGuid();

    private CreateTipUseCase Create => new(_tips, _clock);
    private EditTipUseCase Edit => new(_tips, _clock);
    private SetTipActiveUseCase SetActive => new(_tips, _clock);
    private GetTipsUseCase Get => new(_places, _tips, _tips, _clock);
    private ManageTipFavoritesUseCase Favorites => new(_tips, _tips);

    private Task<AdminTipView> Publish(string title, TipCategory category = TipCategory.Residential) =>
        Create.ExecuteAsync(new SaveTipCommand(_admin, null, title, "Cuerpo del consejo " + title, category), default);

    private async Task PublishMany(int count, TipCategory category = TipCategory.Residential)
    {
        for (var i = 0; i < count; i++) await Publish($"{category} {i}", category);
    }

    private async Task<PlaceView> CreatePlace(PlaceType type, Guid? owner = null)
    {
        var view = await new CreatePlaceUseCase(_places, _geography, _clock).ExecuteAsync(
            new CreatePlaceCommand(owner ?? _user, FakeGeographyReader.Bogota.CityId, "Lugar", type, "Calle 1", null), default);
        _clock.Advance(TimeSpan.FromMinutes(1));
        return view;
    }

    // ── HU-063: create ───────────────────────────────────────────────────

    [Fact]
    public async Task A_new_tip_is_published_at_once_and_remembers_who_wrote_it()
    {
        var tip = await Publish("Cierra la llave");

        Assert.True(tip.IsActive);
        Assert.Equal(_admin, tip.CreatedBy);
        Assert.Equal(TipCategory.Residential, tip.Category);
        Assert.Single(_tips.Tips);
    }

    [Fact]
    public async Task The_form_needs_a_title_and_a_body_within_the_limits()
    {
        Task Save(string title, string body) =>
            Create.ExecuteAsync(new SaveTipCommand(_admin, null, title, body, TipCategory.Residential), default);

        await Assert.ThrowsAsync<InvalidTipException>(() => Save("  ", "texto"));
        await Assert.ThrowsAsync<InvalidTipException>(() => Save("titulo", ""));
        await Assert.ThrowsAsync<InvalidTipException>(() => Save(new string('t', Tip.TitleMaxLength + 1), "texto"));
        await Assert.ThrowsAsync<InvalidTipException>(() => Save("titulo", new string('c', Tip.BodyMaxLength + 1)));
        Assert.Empty(_tips.Tips);
    }

    [Fact]
    public async Task Title_and_body_are_trimmed()
    {
        var tip = await Create.ExecuteAsync(new SaveTipCommand(_admin, null, "  Ahorra  ", "  Cierra la llave.  ", TipCategory.Commercial), default);

        Assert.Equal("Ahorra", tip.Title);
        Assert.Equal("Cierra la llave.", tip.Body);
    }

    // ── HU-064: edit and deactivate ──────────────────────────────────────

    [Fact]
    public async Task Editing_changes_the_tip_at_once_and_records_who_did_it()
    {
        var tip = await Publish("Viejo");
        var other = Guid.NewGuid();
        _clock.Advance(TimeSpan.FromHours(1));

        var edited = await Edit.ExecuteAsync(new SaveTipCommand(other, tip.Id, "Nuevo", "Texto nuevo", TipCategory.Commercial), default);

        Assert.Equal("Nuevo", edited.Title);
        Assert.Equal(TipCategory.Commercial, edited.Category);
        Assert.Equal(other, edited.UpdatedBy);
        Assert.Equal(tip.CreatedBy, edited.CreatedBy);              // the author does not change
        Assert.True(edited.UpdatedAt > edited.CreatedAt);
        var seen = await Get.ExecuteAsync(_user, null, TipCategory.Commercial, null, default);
        Assert.Contains(seen.Tips, t => t.Title == "Nuevo");        // the user sees it right away
    }

    [Fact]
    public async Task Editing_a_tip_that_does_not_exist_is_404()
    {
        await Assert.ThrowsAsync<TipNotFoundException>(() =>
            Edit.ExecuteAsync(new SaveTipCommand(_admin, Guid.NewGuid(), "x", "y", TipCategory.Residential), default));
    }

    [Fact]
    public async Task A_deactivated_tip_disappears_for_the_users_but_is_not_deleted()
    {
        await PublishMany(4);
        var tip = _tips.Tips[0];

        var view = await SetActive.ExecuteAsync(_admin, tip.Id, false, default);

        Assert.False(view.IsActive);
        Assert.Equal(4, _tips.Tips.Count);                          // nothing was removed
        var seen = await Get.ExecuteAsync(_user, null, TipCategory.Residential, 10, default);
        Assert.DoesNotContain(seen.Tips, t => t.Id == tip.Id);
        var admin = await new ListAdminTipsUseCase(_tips).ExecuteAsync(null, true, default);
        Assert.Contains(admin, t => t.Id == tip.Id && !t.IsActive); // the admin still sees it
    }

    [Fact]
    public async Task A_deactivated_tip_can_be_activated_again()
    {
        var tip = await Publish("Vuelve");
        await SetActive.ExecuteAsync(_admin, tip.Id, false, default);

        await SetActive.ExecuteAsync(_admin, tip.Id, true, default);

        Assert.True(_tips.Tips[0].IsActive);
    }

    [Fact]
    public async Task The_admin_list_can_hide_the_inactive_and_filter_by_category()
    {
        await Publish("Casa", TipCategory.Residential);
        var off = await Publish("Local apagado", TipCategory.Commercial);
        await Publish("Local", TipCategory.Commercial);
        await SetActive.ExecuteAsync(_admin, off.Id, false, default);
        var list = new ListAdminTipsUseCase(_tips);

        Assert.Equal(3, (await list.ExecuteAsync(null, true, default)).Count);
        Assert.Equal(2, (await list.ExecuteAsync(null, false, default)).Count);
        Assert.Equal(2, (await list.ExecuteAsync(TipCategory.Commercial, true, default)).Count);
        Assert.Single(await list.ExecuteAsync(TipCategory.Commercial, false, default));
    }

    // ── HU-065: what the user sees ───────────────────────────────────────

    [Fact]
    public async Task A_session_shows_at_least_3_different_tips()
    {
        await PublishMany(12);

        var session = await Get.ExecuteAsync(_user, null, TipCategory.Residential, null, default);

        Assert.True(session.Tips.Count >= 3);
        Assert.Equal(GetTipsUseCase.DefaultCount, session.Tips.Count);
        Assert.Equal(session.Tips.Count, session.Tips.Select(t => t.Id).Distinct().Count());   // all different
        Assert.Equal(12, session.Total);
    }

    [Theory]
    [InlineData(0, 3)]       // asking for fewer than 3 still gives 3
    [InlineData(1, 3)]
    [InlineData(7, 7)]
    [InlineData(99, 10)]     // and never more than 10
    public async Task The_number_of_tips_is_kept_between_3_and_10(int asked, int expected)
    {
        await PublishMany(20);

        Assert.Equal(expected, (await Get.ExecuteAsync(_user, null, TipCategory.Residential, asked, default)).Tips.Count);
    }

    [Fact]
    public async Task With_fewer_than_3_active_tips_it_shows_all_there_are()
    {
        await PublishMany(2);

        Assert.Equal(2, (await Get.ExecuteAsync(_user, null, TipCategory.Residential, null, default)).Tips.Count);
    }

    [Fact]
    public async Task Tips_follow_the_kind_of_place()
    {
        await PublishMany(5, TipCategory.Residential);
        await PublishMany(5, TipCategory.Commercial);
        var home = await CreatePlace(PlaceType.Residential);
        var shop = await CreatePlace(PlaceType.Commercial);

        var forHome = await Get.ExecuteAsync(_user, home.Id, null, null, default);
        var forShop = await Get.ExecuteAsync(_user, shop.Id, null, null, default);

        Assert.Equal(TipCategory.Residential, forHome.Category);
        Assert.All(forHome.Tips, t => Assert.Equal(TipCategory.Residential, t.Category));
        Assert.Equal(TipCategory.Commercial, forShop.Category);
        Assert.All(forShop.Tips, t => Assert.Equal(TipCategory.Commercial, t.Category));
    }

    [Fact]
    public async Task Without_a_place_or_category_it_uses_the_selected_place_and_residential_if_there_is_none()
    {
        await PublishMany(4, TipCategory.Residential);
        await PublishMany(4, TipCategory.Commercial);

        Assert.Equal(TipCategory.Residential, (await Get.ExecuteAsync(_user, null, null, null, default)).Category);

        await CreatePlace(PlaceType.Commercial);                    // the first place is the selected one
        Assert.Equal(TipCategory.Commercial, (await Get.ExecuteAsync(_user, null, null, null, default)).Category);
    }

    [Fact]
    public async Task The_tips_of_a_place_of_someone_else_cannot_be_asked_through_it()
    {
        var place = await CreatePlace(PlaceType.Commercial, owner: Guid.NewGuid());

        await Assert.ThrowsAsync<PlaceNotFoundException>(() => Get.ExecuteAsync(_user, place.Id, null, null, default));
    }

    [Fact]
    public async Task The_order_is_stable_during_the_day_and_changes_on_another_day()
    {
        await PublishMany(30);

        var morning = (await Get.ExecuteAsync(_user, null, TipCategory.Residential, 10, default)).Tips.Select(t => t.Id).ToList();
        _clock.Advance(TimeSpan.FromHours(3));
        var afternoon = (await Get.ExecuteAsync(_user, null, TipCategory.Residential, 10, default)).Tips.Select(t => t.Id).ToList();
        _clock.Advance(TimeSpan.FromDays(1));
        var tomorrow = (await Get.ExecuteAsync(_user, null, TipCategory.Residential, 10, default)).Tips.Select(t => t.Id).ToList();

        Assert.Equal(morning, afternoon);                           // reopening the app shows the same tips
        Assert.NotEqual(morning, tomorrow);                         // tomorrow there are other ones
    }

    // ── HU-065: favorites ────────────────────────────────────────────────

    [Fact]
    public async Task A_user_marks_a_tip_and_sees_the_star_in_the_list()
    {
        await PublishMany(5);
        var tip = _tips.Tips[2];

        var marked = await Favorites.AddAsync(_user, tip.Id, default);

        Assert.True(marked.IsFavorite);
        var session = await Get.ExecuteAsync(_user, null, TipCategory.Residential, 10, default);
        Assert.Equal([tip.Id], session.Tips.Where(t => t.IsFavorite).Select(t => t.Id));
        var mine = await Favorites.ListAsync(_user, default);
        Assert.Equal([tip.Id], mine.Select(t => t.Id));
    }

    [Fact]
    public async Task Favorites_belong_to_each_user()
    {
        await PublishMany(5);
        await Favorites.AddAsync(_user, _tips.Tips[0].Id, default);

        var other = Guid.NewGuid();

        Assert.Empty(await Favorites.ListAsync(other, default));
        var session = await Get.ExecuteAsync(other, null, TipCategory.Residential, 10, default);
        Assert.DoesNotContain(session.Tips, t => t.IsFavorite);
    }

    [Fact]
    public async Task Marking_twice_or_unmarking_what_is_not_marked_changes_nothing()
    {
        await PublishMany(3);
        var tip = _tips.Tips[0];

        await Favorites.AddAsync(_user, tip.Id, default);
        await Favorites.AddAsync(_user, tip.Id, default);
        Assert.Single(_tips.Favorites);

        await Favorites.RemoveAsync(_user, tip.Id, default);
        await Favorites.RemoveAsync(_user, tip.Id, default);
        await Favorites.RemoveAsync(_user, Guid.NewGuid(), default);
        Assert.Empty(_tips.Favorites);
    }

    [Fact]
    public async Task The_newest_favorite_comes_first()
    {
        await PublishMany(3);
        await Favorites.AddAsync(_user, _tips.Tips[0].Id, default);
        await Favorites.AddAsync(_user, _tips.Tips[1].Id, default);

        Assert.Equal([_tips.Tips[1].Id, _tips.Tips[0].Id], (await Favorites.ListAsync(_user, default)).Select(t => t.Id));
    }

    [Fact]
    public async Task A_deactivated_tip_cannot_be_marked_and_leaves_the_favorites()
    {
        await PublishMany(3);
        var tip = _tips.Tips[0];
        await Favorites.AddAsync(_user, tip.Id, default);

        await SetActive.ExecuteAsync(_admin, tip.Id, false, default);

        Assert.Empty(await Favorites.ListAsync(_user, default));                              // gone from the favorites list
        await Assert.ThrowsAsync<TipNotFoundException>(() => Favorites.AddAsync(Guid.NewGuid(), tip.Id, default));
        await SetActive.ExecuteAsync(_admin, tip.Id, true, default);
        Assert.Single(await Favorites.ListAsync(_user, default));                             // and comes back with it
    }

    [Fact]
    public async Task Marking_a_tip_that_does_not_exist_is_404()
    {
        await Assert.ThrowsAsync<TipNotFoundException>(() => Favorites.AddAsync(_user, Guid.NewGuid(), default));
    }
}
