using FluentAssertions;
using Locaccessum.Domain.Entities;

namespace Locaccessum.Tests.Domain;

public class ReservationOverlapTests
{
    static DateTimeOffset T(int h) => new(2026, 1, 1, h, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(9, 11, 10, 12, true)]   // partial overlap
    [InlineData(9, 12, 10, 11, true)]   // b inside a
    [InlineData(9, 10, 10, 11, false)]  // touching at the boundary -> no overlap (half-open)
    [InlineData(9, 10, 11, 12, false)]  // disjoint
    public void Overlaps_is_half_open(int aStart, int aEnd, int bStart, int bEnd, bool expected)
    {
        Reservation.Overlaps(T(aStart), T(aEnd), T(bStart), T(bEnd)).Should().Be(expected);
    }
}
