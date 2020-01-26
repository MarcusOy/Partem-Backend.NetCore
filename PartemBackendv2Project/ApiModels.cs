using System;
namespace PartemBackendProject
{
    public class APIModels
    {
        public class ServiceRequest
        {
            public string Url { get; set; }
        }

        public class ServiceResponse
        {
            public double LeftPercentage { get; set; }
            public double CenterPercentage { get; set; }
            public double RightPercentage { get; set; }

            public string Headline { get; set; }
            public string Source { get; set; }
            public string Image { get; set; }

            public bool Success { get; set; }
            public string Error { get; set; }
        }
    }
}
