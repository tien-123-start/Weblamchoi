using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;  
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace DienLanhWeb.VNPAY
{
    public class VnPayLibrary
    {
        private readonly SortedList<string, string> _requestData = new(new VnPayCompare());
        private readonly SortedList<string, string> _responseData = new(new VnPayCompare());

        public void AddRequestData(string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                _requestData[key] = value;
            }
        }

        public void AddResponseData(string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                _responseData[key] = value;
            }
        }

        public string GetResponseData(string key) =>
            _responseData.TryGetValue(key, out var retValue) ? retValue : string.Empty;

        public string CreateRequestUrl(string baseUrl, string hashSecret)
        {
            var data = new StringBuilder();
            foreach (var kv in _requestData)
            {
                data.Append(WebUtility.UrlEncode(kv.Key) + "=" + WebUtility.UrlEncode(kv.Value) + "&");
            }

            if (data.Length > 0)
            {
                data.Remove(data.Length - 1, 1); // remove last '&'
            }

            string queryString = data.ToString();
            string signData = queryString;
            string vnpSecureHash = Utils.HmacSHA512(hashSecret, signData);

            return $"{baseUrl}?{queryString}&vnp_SecureHash={vnpSecureHash}";
        }

        public bool ValidateSignature(string inputHash, string secretKey)
        {
            string raw = GetResponseRawData();
            string calculatedHash = Utils.HmacSHA512(secretKey, raw);
            return calculatedHash.Equals(inputHash, StringComparison.InvariantCultureIgnoreCase);
        }

        private string GetResponseRawData()
        {
            var data = new StringBuilder();
            _responseData.Remove("vnp_SecureHashType");
            _responseData.Remove("vnp_SecureHash");

            foreach (var kv in _responseData)
            {
                data.Append(WebUtility.UrlEncode(kv.Key) + "=" + WebUtility.UrlEncode(kv.Value) + "&");
            }

            if (data.Length > 0)
            {
                data.Remove(data.Length - 1, 1);
            }

            return data.ToString();
        }
    }

    public static class Utils
    {
        public static string HmacSHA512(string key, string input)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);
            var inputBytes = Encoding.UTF8.GetBytes(input);

            using var hmac = new HMACSHA512(keyBytes);
            var hashBytes = hmac.ComputeHash(inputBytes);

            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }

        public static string GetIpAddress(HttpContext context)
        {
            try
            {
                var ip = context.Connection.RemoteIpAddress;
                if (ip != null && ip.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    ip = Dns.GetHostEntry(ip).AddressList
                        .FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork);
                }

                return ip?.ToString() ?? "127.0.0.1";
            }
            catch
            {
                return "127.0.0.1";
            }
        }
    }

    public class VnPayCompare : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            return string.Compare(x, y, StringComparison.Ordinal);
        }
    }
}
