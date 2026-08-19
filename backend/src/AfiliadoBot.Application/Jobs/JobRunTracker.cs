using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Domain.Enums;
using AfiliadoBot.Infrastructure.Data;

namespace AfiliadoBot.Application.Jobs;

/// <inheritdoc cref="IJobRunTracker" />
public class JobRunTracker : IJobRunTracker
{
    private readonly AfiliadoBotDbContext _dbContext;

    public JobRunTracker(AfiliadoBotDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task RunAsync(JobName jobName, Func<CancellationToken, Task> action, CancellationToken ct)
    {
        var run = JobRun.Start(jobName);
        _dbContext.JobRuns.Add(run);
        await _dbContext.SaveChangesAsync(ct);

        try
        {
            await action(ct);
            run.MarkAsSuccess();
        }
        catch (Exception ex) // inclui OperationCanceledException — nunca deixa o run preso em "Running"
        {
            run.MarkAsFailed(ex.Message);
            throw; // nunca engole excecao — preserva retry do Hangfire e catch dos chamadores
        }
        finally
        {
            // Persiste mesmo se ct foi cancelado (design.md §2.2).
            await _dbContext.SaveChangesAsync(CancellationToken.None);
        }
    }
}
