// <copyright file="AccountService.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Shared.Services;

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Web.Shared.Components;
using MUnique.OpenMU.Web.Shared.Components.Form.Modal;
using MUnique.OpenMU.Web.Shared.Components.Modal;
using MUnique.OpenMU.Web.Shared.Properties;

/// <summary>
/// Service for <see cref="Account"/>s.
/// </summary>
public class AccountService : IDataService<Account>, ISupportDataChangedNotification, IDisposable
{
    private readonly IDataSource<Account> _dataSource;
    private readonly IModalService _modalService;
    private readonly ILoginServer? _loginServer;
    private readonly IServerProvider? _serverProvider;
    private readonly Debouncer _debouncer = new(300);

    private string _searchFilter = string.Empty;

    private AccountState? _stateFilter;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountService"/> class.
    /// </summary>
    /// <param name="dataSource">The player context.</param>
    /// <param name="modalService">The modal service.</param>
    /// <param name="loginServer">The login server, used to disconnect a banned account immediately. Optional: null on hosts where it is not registered (e.g. distributed admin panel without direct server access).</param>
    /// <param name="serverProvider">The server provider, used together with <paramref name="loginServer"/> to find the game server a banned account is connected to.</param>
    public AccountService(IDataSource<Account> dataSource, IModalService modalService, ILoginServer? loginServer = null, IServerProvider? serverProvider = null)
    {
        this._dataSource = dataSource;
        this._modalService = modalService;
        this._loginServer = loginServer;
        this._serverProvider = serverProvider;
    }

    /// <inheritdoc />
    public event EventHandler? DataChanged;

    /// <summary>
    /// Gets or sets the search filter query.
    /// </summary>
    public string SearchFilter
    {
        get => this._searchFilter;
        set
        {
            var newValue = value ?? string.Empty;
            if (this._searchFilter != newValue)
            {
                this._searchFilter = newValue;
                if (string.IsNullOrEmpty(newValue))
                {
                    this._debouncer.Cancel();
                    this.DataChanged?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    _ = this._debouncer.DebounceAsync(this.RaiseDataChangedAsync);
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets the account state to filter the list by. <see langword="null" /> means no filter (all states).
    /// </summary>
    public AccountState? StateFilter
    {
        get => this._stateFilter;
        set
        {
            if (this._stateFilter != value)
            {
                this._stateFilter = value;
                this.DataChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// Returns a slice of the account list, defined by an offset and a count.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="count">The count.</param>
    /// <returns>A slice of the account list, defined by an offset and a count.</returns>
    public async Task<List<Account>> GetAsync(int offset, int count)
    {
        try
        {
            var playerContext = (IPlayerContext)await this._dataSource.GetContextAsync().ConfigureAwait(false);
            var filter = this.SearchFilter.Trim();
            if (string.IsNullOrWhiteSpace(filter))
            {
                return (await playerContext.GetAccountsOrderedByLoginNameAsync(offset, count, this.StateFilter).ConfigureAwait(false)).ToList();
            }

            var results = (await playerContext.SearchAccountsAsync(filter, offset, count, this.StateFilter).ConfigureAwait(false)).ToList();
            if (results.Count == 0 && offset > 0)
            {
                // The filter narrowed the result set down to less entries than the current page offset - show the first page instead.
                results = (await playerContext.SearchAccountsAsync(filter, 0, count, this.StateFilter).ConfigureAwait(false)).ToList();
            }

            return results;
        }
        catch
        {
            return new List<Account>();
        }
    }

    /// <summary>
    /// Bans the specified account and, if it's currently online, disconnects it immediately.
    /// </summary>
    /// <param name="account">The account.</param>
    public async ValueTask BanAsync(Account account)
    {
        account.State = AccountState.Banned;
        var context = await this._dataSource.GetContextAsync().ConfigureAwait(false);
        await context.SaveChangesAsync().ConfigureAwait(false);

        await this.TryDisconnectAsync(account.LoginName).ConfigureAwait(false);

        this.DataChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Unbans the specified account.
    /// </summary>
    /// <param name="account">The account.</param>
    public async ValueTask UnbanAsync(Account account)
    {
        account.State = AccountState.Normal;
        var context = await this._dataSource.GetContextAsync().ConfigureAwait(false);
        await context.SaveChangesAsync().ConfigureAwait(false);
        this.DataChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task TryDisconnectAsync(string loginName)
    {
        if (this._loginServer is null || this._serverProvider is null)
        {
            return;
        }

        try
        {
            var snapshot = await this._loginServer.GetSnapshotAsync().ConfigureAwait(false);
            if (!snapshot.TryGetValue(loginName, out var serverId))
            {
                return; // account is not currently logged in.
            }

            await this._loginServer.LogOffAsync(loginName, serverId).ConfigureAwait(false);
            if (this._serverProvider.Servers.FirstOrDefault(s => s.Id == serverId) is IGameServer gameServer)
            {
                await gameServer.DisconnectAccountAsync(loginName).ConfigureAwait(false);
            }
        }
        catch
        {
            // Best-effort: the ban itself already succeeded and is persisted; a failure to
            // kick an online session immediately just means it'll be rejected on next action
            // or re-login, so we don't want this to surface as a failed ban to the admin.
        }
    }

    /// <summary>
    /// Creates a new Account in a modal dialog.
    /// </summary>
    public async Task CreateNewInModalDialogAsync()
    {
        var accountParameters = new AccountCreationParameters();
        var parameters = new ModalParameters();
        parameters.Add(nameof(ModalCreateNew<AccountCreationParameters>.Item), accountParameters);
        var options = new ModalOptions
        {
            DisableBackgroundCancel = true,
        };

        var modal = this._modalService.Show<ModalCreateNew<AccountCreationParameters>>($"Create {nameof(Account)}", parameters, options);
        var result = await modal.Result.ConfigureAwait(false);
        if (!result.Cancelled)
        {
            var context = await this._dataSource.GetContextAsync().ConfigureAwait(false);
            await context.SaveChangesAsync().ConfigureAwait(false);
            var item = context.CreateNew<Account>();
            item.LoginName = accountParameters.LoginName;
            item.PasswordHash = BCrypt.Net.BCrypt.HashPassword(accountParameters.Password);
            item.EMail = accountParameters.EMail;
            item.State = accountParameters.State;
            item.SecurityCode = accountParameters.SecurityCode;
            item.RegistrationDate = DateTime.UtcNow;
            await context.SaveChangesAsync().ConfigureAwait(false);
            this.DataChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this._debouncer.Dispose();
    }

    private Task RaiseDataChangedAsync()
    {
        this.DataChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Parameters for the account creation which is used for the user interface.
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local", Justification = "Used by data binding.")]
    private class AccountCreationParameters
    {
        [Display(ResourceType = typeof(Resources), Name = nameof(Resources.AccountCreationParameters_LoginName_Name))]
        [MaxLength(10)]
        [MinLength(3)]
        [Required]
        public string LoginName { get; set; } = string.Empty;

        [Display(ResourceType = typeof(Resources), Name = nameof(Resources.AccountCreationParameters_Password_Name))]
        [MaxLength(20)]
        [MinLength(3)]
        [Required]
        public string Password { get; set; } = string.Empty;

        [Display(ResourceType = typeof(Resources), Name = nameof(Resources.AccountCreationParameters_SecurityCode_Name))]
        [MaxLength(10)]
        [MinLength(3)]
        [Required]
        public string SecurityCode { get; set; } = string.Empty;

        [Display(ResourceType = typeof(Resources), Name = nameof(Resources.AccountCreationParameters_EMail_Name))]
        public string EMail { get; set; } = string.Empty;

        [Display(ResourceType = typeof(Resources), Name = nameof(Resources.AccountCreationParameters_State_Name))]
        public AccountState State { get; set; }
    }
}