using HostelSystem.Application.Common;
using HostelSystem.Application.Interfaces;
using MediatR;

namespace HostelSystem.Application.Commands.Applications;

public record RejectApplicationCommand(int ApplicationId, string ReviewedBy, string Reason) : IRequest<Result<bool>>;

public class RejectApplicationHandler : IRequestHandler<RejectApplicationCommand, Result<bool>>
{
    private readonly IApplicationRepository _applicationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RejectApplicationHandler(IApplicationRepository applicationRepository, IUnitOfWork unitOfWork)
    {
        _applicationRepository = applicationRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<bool>> Handle(RejectApplicationCommand cmd, CancellationToken ct)
    {
        var app = await _applicationRepository.GetByIdAsync(cmd.ApplicationId, ct);
        if (app is null)
            return Result<bool>.Fail("Application not found.");

        try
        {
            app.Reject(cmd.ReviewedBy, cmd.Reason);
        }
        catch (Exception ex)
        {
            return Result<bool>.Fail(ex.Message);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        await _unitOfWork.DispatchDomainEventsAsync(ct);
        return Result<bool>.Ok(true);
    }
}
