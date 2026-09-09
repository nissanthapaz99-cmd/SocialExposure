using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SocialExposure.Models;
using SocialExposure.Services;
using SocialExposure.ViewModels;

namespace SocialExposure.Controllers;

[Authorize(Roles = UserRoles.Admin)]
public sealed class AirtableController : Controller
{
    private readonly AirtableOptions _options;
    private readonly AirtableSyncService _syncService;
    private readonly AirtableSyncState _syncState;

    public AirtableController(
        IOptions<AirtableOptions> options,
        AirtableSyncService syncService,
        AirtableSyncState syncState)
    {
        _options = options.Value;
        _syncService = syncService;
        _syncState = syncState;
    }

    [HttpGet]
    public IActionResult Index() => View(new AirtableIntegrationViewModel
    {
        Enabled = _options.Enabled,
        IsConfigured = _options.IsConfigured,
        IsRunning = _syncState.IsRunning,
        BaseId = MaskBaseId(_options.BaseId),
        ClientsTable = _options.ClientsTable,
        EventsTable = _options.EventsTable,
        DesignsTable = _options.DesignsTable,
        SyncIntervalMinutes = Math.Clamp(_options.SyncIntervalMinutes, 1, 1440),
        LastResult = _syncState.LastResult
    });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sync(CancellationToken cancellationToken)
    {
        var result = await _syncService.SyncAllAsync(cancellationToken);
        TempData[result.IsSuccessful ? "SuccessMessage" : "ErrorMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    private static string MaskBaseId(string baseId)
    {
        var value = baseId.Trim();
        return value.Length <= 8 ? value : $"{value[..4]}…{value[^4..]}";
    }
}
