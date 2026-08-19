using FluentAssertions;
using Moq;
using HostelSystem.Application.DTOs;
using HostelSystem.Application.Interfaces;
using HostelSystem.Application.Queries.Rooms;
using HostelSystem.Domain.Entities;

namespace HostelSystem.UnitTests.Application;

public class GetAvailableRoomsQueryHandlerTests
{
    private readonly Mock<IRoomRepository> _rooms = new();
    private readonly Mock<ICacheService> _cache = new();
    private readonly GetAvailableRoomsQueryHandler _sut;

    public GetAvailableRoomsQueryHandlerTests()
    {
        _sut = new GetAvailableRoomsQueryHandler(_rooms.Object, _cache.Object);
    }

    [Fact]
    public async Task Handle_CacheHit_ReturnsCachedList_DoesNotCallRepo()
    {
        var cached = new List<RoomDto>
        {
            new(1, "101A", 4, 0, 500m, true, 1, "Block A")
        };

        _cache.Setup(c => c.GetAsync<List<RoomDto>>("rooms:available:1", default))
              .ReturnsAsync(cached);

        var result = await _sut.Handle(new GetAvailableRoomsQuery(1), default);

        result.Should().BeSameAs(cached);
        _rooms.Verify(r => r.GetAvailableByHostelIdAsync(It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_CacheMiss_QueriesRepoAndCachesResult()
    {
        _cache.Setup(c => c.GetAsync<List<RoomDto>>("rooms:available:1", default))
              .ReturnsAsync((List<RoomDto>?)null);

        var dbRooms = new List<Room> { MakeRoom() };
        _rooms.Setup(r => r.GetAvailableByHostelIdAsync(1, default)).ReturnsAsync(dbRooms);

        var result = await _sut.Handle(new GetAvailableRoomsQuery(1), default);

        result.Should().HaveCount(1);
        result[0].RoomNumber.Should().Be("101A");

        _cache.Verify(c =>
            c.SetAsync("rooms:available:1", It.IsAny<List<RoomDto>>(), TimeSpan.FromSeconds(60), default),
            Times.Once);
    }

    [Fact]
    public async Task Handle_CacheMiss_EmptyDb_ReturnsEmptyList()
    {
        _cache.Setup(c => c.GetAsync<List<RoomDto>>("rooms:available:5", default))
              .ReturnsAsync((List<RoomDto>?)null);

        _rooms.Setup(r => r.GetAvailableByHostelIdAsync(5, default))
              .ReturnsAsync(new List<Room>());

        var result = await _sut.Handle(new GetAvailableRoomsQuery(5), default);

        result.Should().BeEmpty();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Room MakeRoom() => new("101A", 4, 500m, hostelId: 1);
}
