import { useState, useEffect } from 'react';
import { Navigate, Link } from 'react-router-dom';
import useStore from '../store/useStore';
import { 
  Package, DollarSign, MessageSquare, PlusCircle, Map, ShoppingBag, 
  Tag, Users, CheckCircle, Store, Search, Scale, ShieldCheck, Wallet, UserCog
} from 'lucide-react';
import { productsData } from '../data/mockData';

const Dashboard = () => {
  const { currentUser, orders } = useStore();
  const [searchTerm, setSearchTerm] = useState('');
  const [activeTag, setActiveTag] = useState('');
  const [displayedProducts, setDisplayedProducts] = useState([]);
  const [page, setPage] = useState(1);
  const itemsPerPage = 8;
  const tags = ['Y2K', '80s Retro', '90s Vintage'];

  useEffect(() => {
    if (!currentUser || (currentUser.role !== 'CasualUser' && currentUser.role !== 'CasualSeller')) return;
    let filtered = productsData;
    if (searchTerm) {
      filtered = filtered.filter(p => p.name.toLowerCase().includes(searchTerm.toLowerCase()));
    }
    if (activeTag) {
      filtered = filtered.filter(p => p.tags.includes(activeTag));
    } else {
      filtered = [...filtered].sort(() => 0.5 - Math.random());
    }
    setDisplayedProducts(filtered.slice(0, page * itemsPerPage));
  }, [activeTag, searchTerm, page, currentUser]);

  if (!currentUser) {
    return <Navigate to="/login" />;
  }

  const renderCasualUser = () => (
    <div className="space-y-10">
      {/* 1. Search Bar */}
      <div className="max-w-3xl mx-auto">
        <form onSubmit={e => { e.preventDefault(); setPage(1); }} className="flex h-14 rounded-2xl overflow-hidden shadow-lg border border-slate-200 bg-white">
          <input 
            type="text"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            placeholder="Tìm kiếm sản phẩm..."
            className="flex-[3] px-6 focus:outline-none text-slate-700"
          />
          <button type="submit" className="flex-[2] bg-doosii-primary hover:bg-doosii-primary/90 text-white font-bold text-lg flex justify-center items-center gap-2 transition">
            <Search className="w-5 h-5" /> Tìm kiếm
          </button>
        </form>
      </div>

      {/* 2. Horizontal Action Buttons */}
      <div className="flex gap-4 overflow-x-auto pb-4 scrollbar-hide snap-x">
        <Link to="/map" className="snap-start shrink-0 bg-white border p-4 rounded-2xl min-w-[140px] text-center hover:bg-blue-50 hover:border-blue-200 transition group">
          <Map className="w-8 h-8 mx-auto mb-2 text-blue-500 group-hover:scale-110 transition" />
          <span className="font-bold text-sm text-slate-700">Bản đồ (O2O)</span>
        </Link>
        <Link to="/orders" className="snap-start shrink-0 bg-white border p-4 rounded-2xl min-w-[140px] text-center hover:bg-orange-50 hover:border-orange-200 transition group">
          <ShoppingBag className="w-8 h-8 mx-auto mb-2 text-orange-500 group-hover:scale-110 transition" />
          <span className="font-bold text-sm text-slate-700">Quản lý đơn hàng</span>
        </Link>
        <button className="snap-start shrink-0 bg-white border p-4 rounded-2xl min-w-[140px] text-center hover:bg-pink-50 hover:border-pink-200 transition group">
          <Tag className="w-8 h-8 mx-auto mb-2 text-pink-500 group-hover:scale-110 transition" />
          <span className="font-bold text-sm text-slate-700">Pass đồ</span>
        </button>
        <button className="snap-start shrink-0 bg-white border p-4 rounded-2xl min-w-[140px] text-center hover:bg-green-50 hover:border-green-200 transition group">
          <Users className="w-8 h-8 mx-auto mb-2 text-green-500 group-hover:scale-110 transition" />
          <span className="font-bold text-sm text-slate-700">Cộng đồng</span>
        </button>
        <button className="snap-start shrink-0 bg-white border p-4 rounded-2xl min-w-[140px] text-center hover:bg-purple-50 hover:border-purple-200 transition group">
          <CheckCircle className="w-8 h-8 mx-auto mb-2 text-purple-500 group-hover:scale-110 transition" />
          <span className="font-bold text-sm text-slate-700">Xác thực để bán</span>
        </button>
        <button className="snap-start shrink-0 bg-white border p-4 rounded-2xl min-w-[140px] text-center hover:bg-slate-100 transition group">
          <Store className="w-8 h-8 mx-auto mb-2 text-slate-600 group-hover:scale-110 transition" />
          <span className="font-bold text-sm text-slate-700">Quản lý hàng bán</span>
        </button>
      </div>

      {/* 3. Filter Tags */}
      <div className="flex gap-4 justify-center flex-wrap">
        {tags.map(tag => (
          <button
            key={tag}
            onClick={() => { setActiveTag(tag === activeTag ? '' : tag); setPage(1); }}
            className={`px-6 py-3 rounded-xl font-bold transition shadow-sm ${
              activeTag === tag ? 'bg-doosii-secondary text-white scale-105' : 'bg-white text-slate-600 border'
            }`}
          >
            {tag}
          </button>
        ))}
      </div>

      {/* 4. Products Grid */}
      <div className="space-y-8">
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4 md:gap-6">
          {displayedProducts.map(product => (
            <Link to={`/product/${product.id}`} key={`${product.id}-${Math.random()}`} className="glass-card overflow-hidden group hover:shadow-xl transition">
              <div className="relative aspect-square overflow-hidden bg-slate-200">
                <img src={product.image} alt={product.name} className="object-cover w-full h-full group-hover:scale-105 transition duration-500" />
              </div>
              <div className="p-4 space-y-1">
                <h3 className="font-semibold text-slate-800 line-clamp-2 text-sm leading-tight h-10">{product.name}</h3>
                <p className="text-doosii-secondary font-black">
                  {new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(product.price)}
                </p>
              </div>
            </Link>
          ))}
        </div>
        {displayedProducts.length < (activeTag ? productsData.filter(p => p.tags.includes(activeTag)).length : productsData.length) && (
          <div className="text-center pt-4">
            <button onClick={() => setPage(p => p + 1)} className="px-8 py-3 bg-slate-200 text-slate-700 font-bold rounded-full hover:bg-slate-300">
              Tải thêm sản phẩm
            </button>
          </div>
        )}
      </div>
    </div>
  );

  const renderShopper = () => (
    <div className="space-y-10">
      {/* Div 1: Stats */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        <div className="glass-card p-6 flex items-center justify-between border-l-4 border-doosii-primary">
          <div>
            <p className="text-sm text-slate-500 font-bold uppercase mb-1">Đơn chờ đóng gói & Giao</p>
            <p className="text-4xl font-black text-slate-800">12</p>
          </div>
          <Package className="w-12 h-12 text-doosii-primary opacity-20" />
        </div>
        <div className="glass-card p-6 flex items-center justify-between border-l-4 border-blue-500">
          <div>
            <p className="text-sm text-slate-500 font-bold uppercase mb-1">Tin nhắn chưa rep</p>
            <p className="text-4xl font-black text-slate-800">5</p>
          </div>
          <MessageSquare className="w-12 h-12 text-blue-500 opacity-20" />
        </div>
        <div className="glass-card p-6 flex items-center justify-between border-l-4 border-green-500">
          <div>
            <p className="text-sm text-slate-500 font-bold uppercase mb-1">Doanh thu tháng này</p>
            <p className="text-3xl font-black text-green-600">8,450,000đ</p>
          </div>
          <DollarSign className="w-12 h-12 text-green-500 opacity-20" />
        </div>
      </div>

      {/* Div 2: Action Buttons */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        <button className="col-span-2 md:col-span-1 bg-doosii-primary text-white rounded-2xl p-6 flex flex-col items-center justify-center hover:bg-doosii-primary/90 transition shadow-lg shadow-doosii-primary/30 group">
          <PlusCircle className="w-12 h-12 mb-3 group-hover:scale-110 transition" />
          <span className="font-bold text-lg text-center leading-tight">+ Đăng Sản Phẩm Mới</span>
        </button>
        <Link to="/orders" className="bg-white border-2 border-slate-100 rounded-2xl p-6 flex flex-col items-center justify-center hover:border-doosii-secondary transition group text-slate-700">
          <Package className="w-10 h-10 mb-3 text-doosii-secondary group-hover:scale-110 transition" />
          <span className="font-bold text-center">Quản lý Đơn hàng</span>
        </Link>
        <Link to="/chat" className="bg-white border-2 border-slate-100 rounded-2xl p-6 flex flex-col items-center justify-center hover:border-blue-500 transition group text-slate-700">
          <MessageSquare className="w-10 h-10 mb-3 text-blue-500 group-hover:scale-110 transition" />
          <span className="font-bold text-center">Chat Khách hàng</span>
        </Link>
        <button className="bg-white border-2 border-slate-100 rounded-2xl p-6 flex flex-col items-center justify-center hover:border-orange-500 transition group text-slate-700">
          <Map className="w-10 h-10 mb-3 text-orange-500 group-hover:scale-110 transition" />
          <span className="font-bold text-center">Shop trên Bản đồ</span>
        </button>
      </div>
    </div>
  );

  const renderAdmin = () => {
    const totalGMV = orders.reduce((sum, o) => sum + o.total, 0) + 15500000; // mock total
    const escrowHold = 8200000;
    const platformRev = totalGMV * 0.03;

    return (
      <div className="space-y-10">
        {/* Div 1: Stats */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          <div className="glass-card p-6 border-l-4 border-indigo-500">
            <p className="text-sm text-slate-500 font-bold uppercase mb-1">Tổng GMV</p>
            <p className="text-3xl font-black text-slate-800">
              {new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(totalGMV)}
            </p>
          </div>
          <div className="glass-card p-6 border-l-4 border-orange-500 bg-orange-50/30">
            <p className="text-sm text-slate-500 font-bold uppercase mb-1">Dòng tiền Escrow Hold</p>
            <p className="text-3xl font-black text-orange-600">
              {new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(escrowHold)}
            </p>
          </div>
          <div className="glass-card p-6 border-l-4 border-green-500">
            <p className="text-sm text-slate-500 font-bold uppercase mb-1">Doanh thu Nền tảng (3%)</p>
            <p className="text-3xl font-black text-green-600">
              {new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(platformRev)}
            </p>
          </div>
        </div>

        {/* Div 2: Action Buttons */}
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          <button className="bg-white border-2 border-slate-100 rounded-2xl p-6 flex flex-col items-center justify-center hover:border-red-500 transition group text-slate-700">
            <Scale className="w-10 h-10 mb-3 text-red-500 group-hover:scale-110 transition" />
            <span className="font-bold text-center">Tòa án Escrow</span>
          </button>
          <button className="bg-white border-2 border-slate-100 rounded-2xl p-6 flex flex-col items-center justify-center hover:border-blue-500 transition group text-slate-700">
            <ShieldCheck className="w-10 h-10 mb-3 text-blue-500 group-hover:scale-110 transition" />
            <span className="font-bold text-center">Kiểm duyệt (Moderation)</span>
          </button>
          <button className="bg-white border-2 border-slate-100 rounded-2xl p-6 flex flex-col items-center justify-center hover:border-green-500 transition group text-slate-700">
            <Wallet className="w-10 h-10 mb-3 text-green-500 group-hover:scale-110 transition" />
            <span className="font-bold text-center">Tài chính Nền tảng</span>
          </button>
          <button className="bg-white border-2 border-slate-100 rounded-2xl p-6 flex flex-col items-center justify-center hover:border-purple-500 transition group text-slate-700">
            <UserCog className="w-10 h-10 mb-3 text-purple-500 group-hover:scale-110 transition" />
            <span className="font-bold text-center">Người dùng & Cửa hàng</span>
          </button>
        </div>
      </div>
    );
  };

  return (
    <div className="animate-in fade-in">
      <div className="mb-8">
        <h1 className="text-3xl font-bold text-slate-800">Xin chào, {currentUser.fullname}!</h1>
        <p className="text-slate-500">Khu vực quản lý dành cho <span className="font-bold">{currentUser.role}</span></p>
      </div>

      {currentUser.role === 'Admin' && renderAdmin()}
      {currentUser.role === 'Shopper' && renderShopper()}
      {(currentUser.role === 'CasualUser' || currentUser.role === 'CasualSeller') && renderCasualUser()}
    </div>
  );
};

export default Dashboard;
