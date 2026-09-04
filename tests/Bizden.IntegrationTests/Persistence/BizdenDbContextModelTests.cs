using Bizden.Domain.Entities;
using Bizden.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bizden.IntegrationTests.Persistence;

public sealed class BizdenDbContextModelTests
{
    [Fact]
    public void Model_contains_the_required_phase_two_entities_and_constraints()
    {
        var options = new DbContextOptionsBuilder<BizdenDbContext>()
            .UseNpgsql("Host=localhost;Database=bizden_test;Username=postgres")
            .Options;

        using var context = new BizdenDbContext(options);
        var model = context.Model;

        Assert.NotNull(model.FindEntityType(typeof(HostUser)));
        Assert.NotNull(model.FindEntityType(typeof(Event)));
        Assert.NotNull(model.FindEntityType(typeof(Invitation)));
        Assert.NotNull(model.FindEntityType(typeof(Photo)));
        Assert.NotNull(model.FindEntityType(typeof(UploadReservation)));

        var invitation = model.FindEntityType(typeof(Invitation))!;
        Assert.Contains(invitation.GetIndexes(), index => index.IsUnique && index.Properties.Single().Name == nameof(Invitation.TokenHash));

        var reservation = model.FindEntityType(typeof(UploadReservation))!;
        Assert.Contains(reservation.GetIndexes(), index => index.IsUnique && index.Properties.Select(property => property.Name)
            .SequenceEqual([nameof(UploadReservation.InvitationId), nameof(UploadReservation.IdempotencyKey)]));
    }
}
