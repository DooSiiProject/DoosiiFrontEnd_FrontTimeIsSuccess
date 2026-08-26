import { useState } from 'react';
import { Link, Navigate } from 'react-router-dom';
import { ShoppingCart } from 'lucide-react';
import { productsData } from '../data/mockData';
import useStore from '../store/useStore';

const Home = () => {
  const [filter, setFilter] = useState('All');
  const { addToCart, currentUser } = useStore();

  if (currentUser) {
    return <Navigate to="/dashboard" replace />;
  }

  const categories = ['All', ...new Set(productsData.map(p => p.category))];

  const filteredProducts = filter === 'All' 
    ? productsData 
    : productsData.filter(p => p.category === filter);

  return (
    <div className="space-y-8 animate-in fade-in duration-500">
      {/* Hero Banner */}
      <div className="relative rounded-3xl overflow-hidden shadow-2xl h-64 sm:h-80 md:h-96">
        <img 
          src="https://images.unsplash.com/photo-1523381210434-271e8be1f52b?w=1200&auto=format&fit=crop&q=80" 
          alt="DooSii Banner" 
          className="absolute inset-0 w-full h-full object-cover"
        />
        <div className="absolute inset-0 bg-gradient-to-r from-doosii-dark/90 to-transparent flex items-center p-8 md:p-16">
          <div className="max-w-xl text-white space-y-4">
            <h1 className="text-4xl md:text-5xl font-extrabold leading-tight">
              Săn Đồ Si, Không Lo Bị Lừa
            </h1>
            <p className="text-lg opacity-90">
              Nền tảng thanh toán trung gian an toàn tuyệt đối. Mua sắm thông minh cùng cộng đồng Gen-Z.
            </p>
            <Link to="/map" className="inline-block mt-4 px-6 py-3 bg-doosii-primary hover:bg-doosii-primary/90 rounded-full font-semibold transition shadow-lg shadow-doosii-primary/30">
              Khám Phá Bản đồ
            </Link>
          </div>
        </div>
      </div>

      {/* Filters */}
      <div className="flex gap-2 overflow-x-auto pb-2 scrollbar-hide">
        {categories.map(cat => (
          <button
            key={cat}
            onClick={() => setFilter(cat)}
            className={`px-4 py-2 rounded-full whitespace-nowrap transition-all font-medium ${
              filter === cat 
                ? 'bg-doosii-primary text-white shadow-md' 
                : 'bg-white text-slate-600 hover:bg-slate-100'
            }`}
          >
            {cat}
          </button>
        ))}
      </div>

      {/* Product Grid */}
      <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-6">
        {filteredProducts.map(product => (
          <div key={product.id} className="glass-card overflow-hidden group hover:shadow-xl transition-all duration-300 transform hover:-translate-y-1">
            <div className="relative aspect-square overflow-hidden bg-slate-200">
              <img 
                src={product.image} 
                alt={product.name} 
                className="object-cover w-full h-full group-hover:scale-105 transition-transform duration-500"
              />
              <div className="absolute top-2 left-2 bg-white/80 backdrop-blur text-xs font-bold px-2 py-1 rounded-md text-doosii-primary">
                {product.sellType === 'shop' ? 'Shop' : 'Pass'}
              </div>
            </div>
            
            <div className="p-4 space-y-2">
              <h3 className="font-semibold text-slate-800 line-clamp-1" title={product.name}>
                {product.name}
              </h3>
              <p className="text-doosii-secondary font-bold">
                {new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(product.price)}
              </p>
              
              <div className="pt-2 flex justify-between items-center gap-2">
                <Link 
                  to={`/product/${product.id}`}
                  className="flex-1 text-center py-2 bg-slate-100 hover:bg-slate-200 text-sm font-semibold rounded-lg transition"
                >
                  Chi tiết
                </Link>
                <button 
                  onClick={() => addToCart(product)}
                  className="p-2 bg-doosii-primary text-white rounded-lg hover:bg-doosii-primary/90 transition shadow-md"
                >
                  <ShoppingCart className="w-5 h-5" />
                </button>
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
};

export default Home;
