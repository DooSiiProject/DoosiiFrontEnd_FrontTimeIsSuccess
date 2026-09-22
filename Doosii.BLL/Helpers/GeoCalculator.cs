namespace Doosii.BLL.Helpers
{
    public static class GeoCalculator
    {
        private const double EarthRadiusKm = 6371.0;

        /// <summary>
        /// Tinh khoang cach giua 2 toa do (lat, lng) theo cong thuc Haversine (don vi: km).
        /// </summary>
        public static double CalculateDistanceKm(double lat1, double lng1, double lat2, double lng2)
        {
            var dLat = ToRadians(lat2 - lat1);
            var dLng = ToRadians(lng2 - lng1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLng / 2) * Math.Sin(dLng / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return EarthRadiusKm * c;
        }

        /// <summary>
        /// Tinh Bounding Box (minLat, maxLat, minLng, maxLng) quanh toa do (lat, lng) voi ban kinh radiusKm.
        /// Giup loc so bo trong SQL truoc khi tinh Haversine chinh xac trong bo nho.
        /// </summary>
        public static (double MinLat, double MaxLat, double MinLng, double MaxLng) GetBoundingBox(double lat, double lng, double radiusKm)
        {
            // 1 do vi do xap xi 111.045 km
            double deltaLat = radiusKm / 111.045;
            double minLat = lat - deltaLat;
            double maxLat = lat + deltaLat;

            // 1 do kinh do xap xi 111.045 * cos(lat) km
            double cosLat = Math.Cos(ToRadians(lat));
            double deltaLng;
            if (Math.Abs(cosLat) < 0.00001)
            {
                deltaLng = 180.0;
            }
            else
            {
                deltaLng = radiusKm / (111.045 * Math.Abs(cosLat));
            }

            double minLng = lng - deltaLng;
            double maxLng = lng + deltaLng;

            return (
                Math.Max(-90.0, minLat),
                Math.Min(90.0, maxLat),
                Math.Max(-180.0, minLng),
                Math.Min(180.0, maxLng)
            );
        }

        public static double ToRadians(double degrees) => degrees * (Math.PI / 180.0);
    }
}
