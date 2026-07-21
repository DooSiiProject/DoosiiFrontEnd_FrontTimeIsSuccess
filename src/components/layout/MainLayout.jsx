import { Outlet, Link, useNavigate } from 'react-router-dom';
import { ShoppingBag, Bell, MessageCircle, LogOut } from 'lucide-react';
import useStore from '../../store/useStore';

const MainLayout = () => {
  const { currentUser, logout } = useStore();
  const navigate = useNavigate();

  const handleLogout = () => {
    if (window.confirm('Bạn có chắc chắn muốn đăng xuất không?')) {
      logout();
      navigate('/');
    }
  };

  return (
    <div className="min-h-screen flex flex-col bg-slate-50">
      {/* Navbar with Glassmorphism */}
      <nav className="sticky top-0 z-50 glass px-6 py-4 flex justify-between items-center">
        <Link to="/" className="flex items-center gap-2 text-doosii-primary">
          <ShoppingBag className="w-8 h-8" />
          <span className="text-2xl font-bold tracking-tight">DooSii</span>
        </Link>
        
        <div className="flex items-center gap-6">
          {!currentUser ? (
            // Guest View
            <div className="flex items-center gap-3">
              <Link to="/login" className="px-4 py-2 font-semibold text-slate-600 hover:text-doosii-primary transition">
                Đăng nhập
              </Link>
              <Link to="/register" className="px-4 py-2 bg-doosii-primary text-white font-semibold rounded-full hover:bg-doosii-primary/90 transition shadow-md shadow-doosii-primary/20">
                Đăng ký
              </Link>
            </div>
          ) : (
            // Logged In View
            <div className="flex items-center gap-5">
              <button className="text-slate-500 hover:text-doosii-primary transition relative">
                <Bell className="w-6 h-6" />
                <span className="absolute -top-1 -right-1 w-2.5 h-2.5 bg-red-500 rounded-full"></span>
              </button>

              {currentUser.role !== 'Admin' && (
                <Link to="/chat" className="text-slate-500 hover:text-doosii-primary transition">
                  <MessageCircle className="w-6 h-6" />
                </Link>
              )}

              <Link to="/dashboard" className="flex items-center gap-2 pl-2">
                <img src={currentUser.avatar} alt="Avatar" className="w-8 h-8 rounded-full border-2 border-white shadow-sm" />
                <span className="font-bold text-sm hidden md:inline text-slate-700">{currentUser.fullname}</span>
              </Link>

              <button 
                onClick={handleLogout} 
                className="text-slate-400 hover:text-red-500 transition ml-2"
                title="Đăng xuất"
              >
                <LogOut className="w-5 h-5" />
              </button>
            </div>
          )}
        </div>
      </nav>

      {/* Main Content */}
      <main className="flex-grow container mx-auto px-4 py-8 max-w-7xl">
        <Outlet />
      </main>

      {/* Footer */}
      <footer className="glass-dark mt-auto text-white py-8 text-center">
        <p className="opacity-80">© 2026 DooSii. Safe Thrifting Platform.</p>
      </footer>
    </div>
  );
};

export default MainLayout;
