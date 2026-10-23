using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Api.Models;
using Portfolio.Core.Accounts;
using Portfolio.Core.Authentication;
using Portfolio.Core.Management;

namespace Portfolio.Api.Controllers
{
    [ApiController]
    [Authorize]
    [ResponseCache(NoStore = true,
                Location = ResponseCacheLocation.None)]
    [AutoValidateAntiforgeryToken]
    [Route("api")]
    public class PortfolioController : ControllerBase
    {
        private readonly IPortfolioService _portfolioService;
        private readonly IAccountService _accountService;
        private readonly IHistoryReport _historyReport;
        private string OwnerId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        public PortfolioController(IPortfolioService portfolioService, IAccountService accountService,
            IHistoryReport historyReport)
        {
            _portfolioService = portfolioService;
            _accountService = accountService;
            _historyReport = historyReport;
        }

        [HttpGet("accounts/{accountId:int}/portfolios")]
        public async Task<ActionResult<List<PortfolioDetails>>> GetPortfolios(int accountId)
        {
            return Ok(await _portfolioService.GetPortfolios(OwnerId, accountId));
        }

        [HttpGet("accounts/{accountId:int}/portfolios/{portfolioId:int}")]
        public async Task<ActionResult<PortfolioDetails>> GetPortfolio(int accountId, int portfolioId)
        {
            List<PortfolioDetails> portfolios = await _portfolioService.GetPortfolios(OwnerId, accountId);
            PortfolioDetails? portfolio = portfolios.FirstOrDefault(p => p.Id == portfolioId);

            if (portfolio == null)
            {
                return NotFound();
            }

            return Ok(portfolio);
        }

        [HttpPost("accounts/{accountId:int}/portfolios")]
        public async Task<ActionResult<PortfolioDetails>> CreatePortfolio(int accountId, AccountRequest request)
        {
            PortfolioDetails portfolio = await _portfolioService.CreatePortfolio(OwnerId, accountId, request.Name);
            return CreatedAtAction(nameof(GetPortfolio), new { accountId, portfolioId = portfolio.Id }, portfolio);
        }

        [HttpPut("accounts/{accountId:int}/portfolios/{portfolioId:int}")]
        public async Task<ActionResult<PortfolioDetails>> RenamePortfolio(int accountId, int portfolioId, AccountRequest request)
        {
            return Ok(await _portfolioService.RenamePortfolio(OwnerId, accountId, portfolioId, request.Name));
        }

        [HttpGet("accounts/{accountId:int}/portfolios/{portfolioId:int}/holdings")]
        public async Task<ActionResult<List<HoldingDetails>>> GetHoldings(int accountId, int portfolioId)
        {
            return Ok(await _portfolioService.GetHoldings(OwnerId, accountId, portfolioId));
        }

        [HttpPost("accounts/{accountId:int}/cash-transactions")]
        public async Task<ActionResult<TransactionDetails>> RecordCash(int accountId, CashRequest request)
        {
            TransactionDetails transaction = await _portfolioService.RecordCash(OwnerId, accountId, request.Type, request.Amount);
            return StatusCode(201, transaction);
        }

        [HttpPost("accounts/{accountId:int}/portfolios/{portfolioId:int}/trades")]
        public async Task<ActionResult<TransactionDetails>> RecordTrade(int accountId, int portfolioId, TradeRequest request)
        {
            TransactionDetails transaction = await _portfolioService.RecordTrade(OwnerId, accountId, portfolioId,
                request.InvestmentId, request.Type, request.Quantity);
            return StatusCode(201, transaction);
        }

        [HttpGet("accounts/{accountId:int}/history")]
        public async Task<ActionResult<List<TransactionDetails>>> GetHistory(int accountId)
        {
            if (await _accountService.GetAccount(OwnerId, accountId) == null)
            {
                return NotFound();
            }

            return Ok(await _historyReport.GetHistory(OwnerId, accountId));
        }

        [HttpGet("dashboard")]
        public async Task<ActionResult<DashboardDetails>> GetDashboard()
        {
            return Ok(await _portfolioService.GetDashboard(OwnerId));
        }

        [HttpGet("investments")]
        public async Task<ActionResult<List<InvestmentDetails>>> GetInvestments()
        {
            return Ok(await _portfolioService.GetInvestments());
        }

        [Authorize(Roles = ApplicationRoles.Admin)]
        [HttpPost("investments")]
        public async Task<ActionResult<InvestmentDetails>> CreateInvestment(InvestmentRequest request)
        {
            return StatusCode(201, await _portfolioService.CreateInvestment(request.Symbol, request.Name, request.Price));
        }

        [Authorize(Roles = ApplicationRoles.Admin)]
        [HttpPut("investments/{id:int}")]
        public async Task<ActionResult<InvestmentDetails>> UpdateInvestment(int id, InvestmentUpdateRequest request)
        {
            return Ok(await _portfolioService.UpdateInvestment(id, request.Price));
        }
    }
}
