import { useState } from 'react';
import { Link, useNavigate, Navigate } from 'react-router-dom';
import useStore from '../store/useStore';
import { ShoppingBag } from 'lucide-react';

const Login = () => {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const { login, currentUser } = useStore();
  const navigate = useNavigate();

  if (currentUser) {
    return <Navigate to="/dashboard" replace />;
  }

  const handleLogin = (e) => {
    e.preventDefault();
    const success = login(email, password);
    if (success) {
      navigate('/dashboard');
    } else {
      setError('Email hoặc mật khẩu không chính xác!');
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center p-4 bg-slate-50">
      <div className="glass-card max-w-md w-full p-8 space-y-6 shadow-2xl">
        <div className="text-center">
          <Link to="/" className="inline-flex items-center gap-2 text-doosii-primary mb-4">
            <ShoppingBag className="w-10 h-10" />
          </Link>
          <h2 className="text-3xl font-extrabold text-slate-900">Đăng nhập</h2>
          <p className="text-slate-500 mt-2">Chào mừng trở lại với DooSii</p>
        </div>

        {error && <div className="bg-red-50 text-red-500 p-3 rounded-lg text-sm text-center font-medium">{error}</div>}

        <form onSubmit={handleLogin} className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-slate-700 mb-1">Email</label>
            <input 
              type="email" 
              required
              value={email}
              onChange={e => setEmail(e.target.value)}
              className="w-full px-4 py-3 rounded-xl border border-slate-200 focus:ring-2 focus:ring-doosii-primary focus:outline-none transition"
              placeholder="you@example.com"
            />
          </div>
          <div>
            <div className="flex justify-between items-center mb-1">
              <label className="block text-sm font-medium text-slate-700">Mật khẩu</label>
              <a href="#" className="text-xs text-doosii-primary hover:underline font-medium">Quên mật khẩu?</a>
            </div>
            <input 
              type="password" 
              required
              value={password}
              onChange={e => setPassword(e.target.value)}
              className="w-full px-4 py-3 rounded-xl border border-slate-200 focus:ring-2 focus:ring-doosii-primary focus:outline-none transition"
              placeholder="••••••••"
            />
          </div>

          <button 
            type="submit" 
            className="w-full bg-doosii-primary text-white py-3 rounded-xl font-bold hover:bg-doosii-primary/90 transition shadow-lg shadow-doosii-primary/30"
          >
            Đăng nhập
          </button>
        </form>

        <div className="text-center text-sm text-slate-500">
          Bạn chưa có tài khoản? <Link to="/register" className="font-bold text-doosii-primary hover:underline">Đăng ký ngay</Link>
        </div>
        
        <div className="text-center pt-4 border-t border-slate-100">
          <p className="text-xs text-slate-500 mb-2 font-medium">Đăng nhập nhanh (Dành cho Tester):</p>
          <div className="grid grid-cols-2 gap-2 mb-4">
            <button type="button" onClick={() => { setEmail('AnyaWithThinkingSenne@gmail.com'); setPassword('Chunnimommy'); }} className="text-xs py-2 bg-pink-50 text-pink-600 rounded-lg font-bold hover:bg-pink-100 transition">Buyer A (Casual)</button>
            <button type="button" onClick={() => { setEmail('Loidthichloichoi@gmail.com'); setPassword('LoidisLoid'); }} className="text-xs py-2 bg-blue-50 text-blue-600 rounded-lg font-bold hover:bg-blue-100 transition">Buyer B (Seller)</button>
            <button type="button" onClick={() => { setEmail('YorGoldForGirlForMyForgerFamilu@gmail.com'); setPassword('YorGirlShopshoping97'); }} className="text-xs py-2 bg-purple-50 text-purple-600 rounded-lg font-bold hover:bg-purple-100 transition">Shopper</button>
            <button type="button" onClick={() => { setEmail('MailMailGoGo@gmail.com'); setPassword('Letgogo11'); }} className="text-xs py-2 bg-slate-100 text-slate-600 rounded-lg font-bold hover:bg-slate-200 transition">Admin</button>
          </div>
          <Link to="/" className="text-sm font-medium text-slate-600 hover:text-slate-900 transition">
            &larr; Về lại trang chủ (Guest)
          </Link>
        </div>
      </div>
    </div>
  );
};

export default Login;
