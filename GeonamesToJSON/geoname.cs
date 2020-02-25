using System;

namespace GeonamesToJSON
{
    public class geonameFlat
    {
#nullable enable
        public long? geonameId { get; set; }
        public string? name { get; set; }
        public string? asciiName { get; set; }
        public string[]? alternateNames { get; set; }
        public decimal? latitude { get; set; }
        public decimal? longitude { get; set; }
        public string? feature { get; set; }
        public string? countryCode { get; set; }
        public string? countryCodeISO3 { get; set; }
        public string? country { get; set; }
        public string? continent { get; set; }
        public string? tld { get; set; }
        public string? currencyCode { get; set; }
        public string? currency { get; set; }
        public string[]? languages { get; set; }
        public string? alternateCountryCode { get; set; }
        public string? admin1 { get; set; }
        public string? admin2 { get; set; }
        public long? population { get; set; }
        public long? elevation { get; set; }
        public string? timezone { get; set; }
        public string? windowsTimezone { get; set; }
        public string[]? neighborCountries { get; set; }
        public DateTime? dateModified { get; set; }
#nullable disable
    }

    public class geonameStructured
    {
        public string countryCode { get; set; }
        public string admin1Code { get; set; }
        public string admin2Code { get; set; }
        public string tempFilename { get; set; }
        public string key
        {
            get
            {
                return $"{countryCode}-{admin1Code}-{admin2Code}";
            }
        }
    }
}
