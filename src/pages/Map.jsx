import { useState, useRef, useEffect } from 'react';
import { MapContainer, TileLayer, Marker, Popup, Polyline } from 'react-leaflet';
import 'leaflet/dist/leaflet.css';
import L from 'leaflet';
import { shopsData } from '../data/mockData';
import { Link, useLocation } from 'react-router-dom';

// Fix for default Leaflet icon in React
import icon from 'leaflet/dist/images/marker-icon.png';
import iconShadow from 'leaflet/dist/images/marker-shadow.png';

let DefaultIcon = L.icon({
    iconUrl: icon,
    shadowUrl: iconShadow,
    iconSize: [25, 41],
    iconAnchor: [12, 41]
});
L.Marker.prototype.options.icon = DefaultIcon;

const userIcon = L.divIcon({
  className: 'custom-user-icon',
  html: `<div style="background-color: #ef4444; width: 16px; height: 16px; border-radius: 50%; border: 3px solid white; box-shadow: 0 0 5px rgba(0,0,0,0.5);"></div>`,
  iconSize: [16, 16],
  iconAnchor: [8, 8]
});

const MapPage = () => {
  const location = useLocation();
  const targetShopId = location.state?.shopId;
  const markerRefs = useRef({});

  // Tọa độ trung tâm (Thủ Đức - Dĩ An)
  const center = [10.8600, 106.7700];
  const userLocation = [10.8757, 106.7990]; // Nhà Văn hóa Sinh viên ĐHQG, Dĩ An, Bình Dương
  const [activeShop, setActiveShop] = useState(null);
  const [selectedRoute, setSelectedRoute] = useState(null);
  const [routeInfo, setRouteInfo] = useState(null);

  useEffect(() => {
    if (targetShopId && markerRefs.current[targetShopId]) {
      setTimeout(() => {
        markerRefs.current[targetShopId].openPopup();
      }, 500);
    }
  }, [targetShopId]);

  // Haversine formula to calculate distance in km
  const calculateDistance = (lat1, lon1, lat2, lon2) => {
    const R = 6371; 
    const dLat = (lat2 - lat1) * Math.PI / 180;  
    const dLon = (lon2 - lon1) * Math.PI / 180; 
    const a = 
      Math.sin(dLat/2) * Math.sin(dLat/2) +
      Math.cos(lat1 * Math.PI / 180) * Math.cos(lat2 * Math.PI / 180) * 
      Math.sin(dLon/2) * Math.sin(dLon/2); 
    const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1-a)); 
    return R * c; 
  };

  return (
    <div className="space-y-6 animate-in fade-in duration-500">
      <div>
        <h1 className="text-3xl font-bold text-slate-800">O2O Hidden Gem Map</h1>
        <p className="text-slate-500 mt-2">Khám phá các cửa hàng đồ si chất lượng xung quanh khu vực Thủ Đức.</p>
      </div>

      <div className="glass-card overflow-hidden border-2 border-white/60 relative h-[600px] w-full shadow-2xl rounded-2xl z-0">
        <MapContainer center={center} zoom={14} scrollWheelZoom={true} className="h-full w-full">
          <TileLayer
            attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
            url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
          />
          
          <Marker position={userLocation} icon={userIcon}>
            <Popup>
              <div className="text-center p-1">
                <strong className="text-red-500 font-bold">Vị trí của bạn</strong><br/>
                <span className="text-xs text-slate-600">Nhà Văn hóa Sinh viên ĐHQG</span>
              </div>
            </Popup>
          </Marker>

          {selectedRoute && (
            <Polyline 
              positions={selectedRoute} 
              color="#4F46E5" 
              weight={4} 
              opacity={0.8} 
              dashArray="8, 8" 
            />
          )}
          
          {shopsData.map((shop) => (
            <Marker 
              key={shop.id} 
              position={[shop.lat, shop.lng]}
              ref={(ref) => {
                if (ref) markerRefs.current[shop.id] = ref;
              }}
              eventHandlers={{
                click: () => setActiveShop(shop),
              }}
            >
              <Popup className="rounded-xl overflow-hidden shadow-lg">
                <div className="p-1 min-w-[200px]">
                  <div className="flex items-center gap-3 mb-3">
                    <img src={shop.logoAvatar} alt={shop.name} className="w-12 h-12 rounded-full shadow-md object-cover" />
                    <div>
                      <h3 className="font-bold text-lg text-slate-800">{shop.name}</h3>
                      <span className="inline-block px-2 py-0.5 bg-green-100 text-green-700 text-xs font-semibold rounded-full">
                        {shop.status.toUpperCase()}
                      </span>
                    </div>
                  </div>
                  <p className="text-sm text-slate-600 mb-3">Thành lập: {shop.establishedDate}</p>
                  
                  <button 
                    onClick={() => {
                      setSelectedRoute([userLocation, [shop.lat, shop.lng]]);
                      const dist = calculateDistance(userLocation[0], userLocation[1], shop.lat, shop.lng);
                      const timeMinutes = Math.round((dist / 40) * 60);
                      setRouteInfo({
                        distance: dist.toFixed(1),
                        time: timeMinutes
                      });
                    }}
                    className="block w-full text-center bg-white border-2 border-indigo-500 text-indigo-600 py-1.5 mb-2 rounded-lg font-bold hover:bg-indigo-50 transition"
                  >
                    Xác định vị trí (Chỉ đường)
                  </button>

                  <Link 
                    to="/" 
                    className="block w-full text-center bg-doosii-primary text-white py-2 rounded-lg font-medium hover:bg-doosii-primary/90 transition"
                  >
                    Xem sản phẩm
                  </Link>
                </div>
              </Popup>
            </Marker>
          ))}
        </MapContainer>
      </div>

      {routeInfo && (
        <div className="glass-card p-6 border-l-4 border-indigo-500 flex flex-col md:flex-row items-start md:items-center justify-between shadow-lg gap-4">
          <div>
            <h3 className="font-bold text-lg text-slate-800">Thông tin lộ trình (Đường chim bay)</h3>
            <p className="text-slate-600">Dựa trên vận tốc trung bình xe máy là 40km/h.</p>
          </div>
          <div className="flex gap-8 text-left md:text-right w-full md:w-auto">
            <div>
              <p className="text-sm text-slate-500 font-bold uppercase">Khoảng cách</p>
              <p className="text-3xl font-black text-indigo-600">{routeInfo.distance} km</p>
            </div>
            <div>
              <p className="text-sm text-slate-500 font-bold uppercase">Thời gian dự kiến</p>
              <p className="text-3xl font-black text-indigo-600">
                {routeInfo.time > 60 
                  ? `${Math.floor(routeInfo.time / 60)} giờ ${routeInfo.time % 60} phút` 
                  : `${routeInfo.time} phút`}
              </p>
            </div>
          </div>
        </div>
      )}

    </div>
  );
};

export default MapPage;
