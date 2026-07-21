import { useState } from 'react';
import { Link, useNavigate, Navigate } from 'react-router-dom';
import useStore from '../store/useStore';
import { ShoppingBag } from 'lucide-react';

const Register = () => {
  const [fullname, setFullname] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error, setError] = useState('');
  const { register, currentUser } = useStore();
  const navigate = useNavigate();

  if (currentUser) {
    return <Navigate to="/dashboard" replace />;
  }

  const handleRegister = (e) => {
    e.preventDefault();
    if (password !== confirmPassword) {
      setError('Mật khẩu xác nhận không khớp!');
      return;
    }
    const success = register({ fullname, email, password });
    if (success) {
      alert('Đăng ký thành công! Vui lòng đăng nhập.');
      navigate('/login');
    } else {
      setError('Email này đã được sử dụng!');
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center p-4 bg-slate-50">
      <div className="glass-card max-w-md w-full p-8 space-y-6 shadow-2xl">
        <div className="text-center">
          <Link to="/" className="inline-flex items-center gap-2 text-doosii-primary mb-4">
            <ShoppingBag className="w-10 h-10" />
          </Link>
          <h2 className="text-3xl font-extrabold text-slate-900">Tạo tài khoản mới</h2>
          <p className="text-slate-500 mt-2">Gia nhập cộng đồng DooSii ngay hôm nay</p>
        </div>

        {error && <div className="bg-red-50 text-red-500 p-3 rounded-lg text-sm text-center font-medium">{error}</div>}

        <form onSubmit={handleRegister} className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-slate-700 mb-1">Họ và tên</label>
            <input 
              type="text" 
              required
              value={fullname}
              onChange={e => setFullname(e.target.value)}
              className="w-full px-4 py-3 rounded-xl border border-slate-200 focus:ring-2 focus:ring-doosii-primary focus:outline-none transition"
              placeholder="Nguyễn Văn A"
            />
          </div>
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
            <label className="block text-sm font-medium text-slate-700 mb-1">Mật khẩu</label>
            <input 
              type="password" 
              required
              value={password}
              onChange={e => setPassword(e.target.value)}
              className="w-full px-4 py-3 rounded-xl border border-slate-200 focus:ring-2 focus:ring-doosii-primary focus:outline-none transition"
              placeholder="••••••••"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-700 mb-1">Xác nhận mật khẩu</label>
            <input 
              type="password" 
              required
              value={confirmPassword}
              onChange={e => setConfirmPassword(e.target.value)}
              className="w-full px-4 py-3 rounded-xl border border-slate-200 focus:ring-2 focus:ring-doosii-primary focus:outline-none transition"
              placeholder="••••••••"
            />
          </div>

          <button 
            type="submit" 
            className="w-full bg-doosii-primary text-white py-3 rounded-xl font-bold hover:bg-doosii-primary/90 transition shadow-lg shadow-doosii-primary/30 mt-2"
          >
            Đăng ký
          </button>
        </form>
        
        <div className="text-center text-sm text-slate-500">
          Đã có tài khoản? <Link to="/login" className="font-bold text-doosii-primary hover:underline">Đăng nhập</Link>
        </div>
      </div>
    </div>
  );
};

export default Register;
