import { useParams, Link, useNavigate } from 'react-router-dom';
import { ShoppingCart, ShieldCheck, Video, MessageSquare, MapPin } from 'lucide-react';
import { productsData, shopsData, usersData } from '../data/mockData';
import useStore from '../store/useStore';

const ProductDetail = () => {
  const { id } = useParams();
  const navigate = useNavigate();
  const { currentUser, setCheckoutProduct } = useStore();
  
  const product = productsData.find(p => p.id === parseInt(id));
  
  if (!product) {
    return <div className="text-center py-20 text-2xl font-bold text-slate-500">Sản phẩm không tồn tại</div>;
  }

  const sellerInfo = product.sellType === 'shop' 
    ? shopsData.find(s => s.id === product.shopId)
    : usersData.find(u => u.id === product.sellerId);

  return (
    <div className="max-w-6xl mx-auto space-y-8 animate-in slide-in-from-bottom-8 duration-500">
      <div className="grid grid-cols-1 md:grid-cols-2 gap-10">
        
        {/* Product Image */}
        <div className="rounded-3xl overflow-hidden shadow-2xl bg-white border border-slate-100 aspect-[4/5]">
          <img 
            src={product.image} 
            alt={product.name} 
            className="w-full h-full object-cover hover:scale-105 transition-transform duration-700 cursor-crosshair"
          />
        </div>

        {/* Product Info */}
        <div className="space-y-6">
          <div className="space-y-2">
            <div className="flex gap-2 flex-wrap">
              {product.tags.map(tag => (
                <span key={tag} className="px-3 py-1 bg-slate-100 text-slate-600 text-xs font-bold uppercase tracking-wider rounded-full">
                  {tag}
                </span>
              ))}
            </div>
            <h1 className="text-4xl font-extrabold text-slate-900 leading-tight">{product.name}</h1>
            <p className="text-3xl text-doosii-secondary font-black">
              {new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(product.price)}
            </p>
          </div>

          <div className="glass-card p-5 space-y-4">
            <h3 className="font-bold text-lg text-slate-800">Thông tin người bán</h3>
            <div className="flex items-center gap-4">
              <img 
                src={product.sellType === 'shop' ? sellerInfo?.logoAvatar : sellerInfo?.avatar} 
                alt="Seller" 
                className="w-16 h-16 rounded-full shadow-md object-cover border-2 border-white"
              />
              <div>
                <h4 className="font-bold text-xl text-slate-800">
                  {product.sellType === 'shop' ? sellerInfo?.name : sellerInfo?.fullname}
                </h4>
                <div className="flex items-center gap-2 mt-1">
                  <span className="px-2 py-0.5 bg-doosii-primary/10 text-doosii-primary text-xs font-bold rounded">
                    {product.sellType === 'shop' ? 'Shop Chuyên Nghiệp' : 'Cá Nhân Pass'}
                  </span>
                </div>
              </div>
            </div>
            
            <div className="flex gap-2 mt-4">
              <button 
                onClick={() => {
                  if (!currentUser) return alert('Vui lòng đăng nhập!');
                  const sellerId = product.sellType === 'shop' ? sellerInfo.ownerId : sellerInfo.id;
                  const convId = useStore.getState().createConversation(currentUser.id, sellerId, product.id);
                  navigate('/chat', { state: { activeConvId: convId } });
                }}
                className="flex-1 flex items-center justify-center gap-2 bg-slate-100 hover:bg-slate-200 text-slate-700 py-2 rounded-xl font-medium transition"
              >
                <MessageSquare className="w-4 h-4" /> Chat thương lượng
              </button>
              {product.sellType === 'shop' && (
                <button 
                  onClick={() => navigate('/map', { state: { shopId: product.shopId } })}
                  className="flex-1 flex items-center justify-center gap-2 bg-indigo-50 hover:bg-indigo-100 text-indigo-600 py-2 rounded-xl font-medium transition border border-indigo-100"
                >
                  <MapPin className="w-4 h-4" /> Xem vị trí shop
                </button>
              )}
            </div>
          </div>

          <div className="space-y-4">
            <h3 className="font-bold text-lg text-slate-800 border-b pb-2">Chi tiết sản phẩm</h3>
            <div className="grid grid-cols-2 gap-y-3 gap-x-6 text-sm">
              <p><span className="text-slate-500">Thương hiệu:</span> <span className="font-semibold">{product.brand}</span></p>
              <p><span className="text-slate-500">Xuất xứ:</span> <span className="font-semibold">{product.origin}</span></p>
              <p><span className="text-slate-500">Chất liệu:</span> <span className="font-semibold">{product.material}</span></p>
              <p><span className="text-slate-500">Màu sắc:</span> <span className="font-semibold">{product.colors.join(', ')}</span></p>
            </div>
            
            <h4 className="font-semibold text-slate-700 mt-4">Thông số đo lường</h4>
            <div className="flex flex-wrap gap-2">
              {Object.entries(product.specs).map(([key, value]) => (
                <span key={key} className="px-3 py-1 bg-white border border-slate-200 rounded-lg text-sm shadow-sm">
                  <span className="text-slate-500 capitalize">{key.replace(/([A-Z])/g, ' $1').trim()}:</span> <span className="font-bold">{value} cm</span>
                </span>
              ))}
            </div>

            <p className="text-slate-700 leading-relaxed bg-slate-50 p-4 rounded-xl border border-slate-100">
              {product.description}
            </p>
          </div>

          {/* Action Buttons */}
          <div className="pt-6 border-t flex flex-col gap-3">
            <button 
              onClick={() => {
                setCheckoutProduct(product);
                navigate('/checkout');
              }}
              className="w-full bg-doosii-primary text-white py-4 rounded-2xl font-bold text-lg hover:bg-doosii-primary/90 transition shadow-xl shadow-doosii-primary/30 flex items-center justify-center gap-2"
            >
              <ShoppingCart className="w-6 h-6" /> Mua Ngay (Escrow)
            </button>
            <div className="flex justify-center gap-6 text-sm text-slate-500 font-medium">
              <span className="flex items-center gap-1"><ShieldCheck className="w-4 h-4 text-green-500" /> Thanh toán an toàn</span>
              <span className="flex items-center gap-1"><Video className="w-4 h-4 text-blue-500" /> Hỗ trợ đồng kiểm video</span>
            </div>
          </div>
          
        </div>
      </div>
    </div>
  );
};

export default ProductDetail;
