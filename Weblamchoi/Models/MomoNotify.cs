namespace weblamchoi.Models
{
    public class MomoNotify
    {
        public string partnerCode { get; set; } = "";
        public string accessKey { get; set; } = "";
        public string requestId { get; set; } = "";
        public string orderId { get; set; } = "";
        public string extraData { get; set; } = "";

        public long amount { get; set; } // <-- fix
        public long transId { get; set; } // <-- fix
        public string payType { get; set; } = "";
        public int resultCode { get; set; } // <-- fix
        public string message { get; set; } = "";
        public long responseTime { get; set; } // <-- fix
        public string signature { get; set; } = "";
        public string orderInfo { get; set; } = "";
        public string orderType { get; set; } = "";
    }
}
