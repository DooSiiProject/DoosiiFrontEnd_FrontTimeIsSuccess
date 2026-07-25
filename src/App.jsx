import { BrowserRouter, Routes, Route } from 'react-router-dom';
import MainLayout from './components/layout/MainLayout';
import Home from './pages/Home';
import MapPage from './pages/Map';
import ProductDetail from './pages/ProductDetail';

import Checkout from './pages/Checkout';
import Dashboard from './pages/Dashboard';
import Orders from './pages/Orders';
import Login from './pages/Login';
import Register from './pages/Register';
import Chat from './pages/Chat';
import HoldManagement from './pages/HoldManagement';

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<Login />} />
        <Route path="/register" element={<Register />} />
        
        <Route path="/" element={<MainLayout />}>
          <Route index element={<Home />} />
          <Route path="map" element={<MapPage />} />
          <Route path="product/:id" element={<ProductDetail />} />

          <Route path="checkout" element={<Checkout />} />
          <Route path="dashboard" element={<Dashboard />} />
          <Route path="orders" element={<Orders />} />
          <Route path="holds" element={<HoldManagement />} />
          <Route path="chat" element={<Chat />} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
}

export default App;
