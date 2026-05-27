using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TMH.Shared.DTOs
{
    public class VnPaymentRequestDto
    {
        public int AppointmentId { get; set; }
        public double Amount { get; set; }
        public string OrderInfo { get; set; } = string.Empty;
    }

    public class VnPaymentResponseDto
    {
        public bool Success { get; set; }
        public string PaymentMethod { get; set; } = "VnPay";
        public string OrderDescription { get; set; } = string.Empty;
        public string OrderId { get; set; } = string.Empty;
        public string TransactionId { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string VnPayResponseCode { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}