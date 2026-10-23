using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Api.Models;
using Portfolio.Core.Accounts;

namespace Portfolio.Api.Controllers
{
    [ApiController]
    [Route("api/accounts")]
    [Authorize]
    [ResponseCache(NoStore = true,
                Location = ResponseCacheLocation.None)]
    [AutoValidateAntiforgeryToken]
    public class AccountsController : ControllerBase
    {
        private readonly IAccountService _accountService;
        private string OwnerId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        public AccountsController(IAccountService accountService)
        {
            _accountService = accountService;
        }

        [HttpGet]
        public async Task<ActionResult<List<AccountDetails>>> GetAccounts()
        {
            return Ok(await _accountService.GetAccounts(OwnerId));
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<AccountDetails>> GetAccount(int id)
        {
            AccountDetails? account = await _accountService.GetAccount(OwnerId, id);

            if (account == null)
            {
                return NotFound();
            }

            return Ok(account);
        }

        [HttpPost]
        public async Task<ActionResult<AccountDetails>> CreateAccount(AccountRequest request)
        {
            AccountDetails account = await _accountService.CreateAccount(OwnerId, request.Name);
            return CreatedAtAction(nameof(GetAccount), new { id = account.Id }, account);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<AccountDetails>> RenameAccount(int id, AccountRequest request)
        {
            AccountDetails? account = await _accountService.RenameAccount(OwnerId, id, request.Name);

            if (account == null)
            {
                return NotFound();
            }

            return Ok(account);
        }
    }
}
