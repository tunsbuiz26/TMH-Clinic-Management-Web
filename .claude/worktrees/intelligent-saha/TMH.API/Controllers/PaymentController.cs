using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TMH.API.Services;
using TMH.Shared.DTOs;

namespace TMH.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {
        private readonly VnPayService _vnPayService;

        public PaymentController(VnPayService vnPayService)
        {
            _vnPayService = vnPayService;
        }

        // POST /api/payment/create-payment-url
        [HttpPost("create-payment-url")]
        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> CreatePaymentUrl([FromBody] VnPaymentRequestDto dto)
        {
            var paymentUrl = await _vnPayService.CreatePaymentUrl(HttpContext, dto);
            return Ok(new { url = paymentUrl });
        }

        // GET /api/payment/payment-return
        [HttpGet("payment-return")]
        [AllowAnonymous]
        public async Task<IActionResult> PaymentReturn()
        {
            var response = await _vnPayService.PaymentExecute(Request.Query);
            return Ok(response);
        }
    }
}