import React, { useState } from 'react';
import { Navigate } from 'react-router-dom';
import { ShoppingBag, Store, Clock, Check, X, ShieldCheck } from 'lucide-react';
import useStore from '../store/useStore';
import { productsData, shopsData } from '../data/mockData';

const HoldManagement = () => {
  const { currentUser, holdRequests, updateHoldRequestStatus, users } = useStore();
  const [statusFilter, setStatusFilter] = useState('all'); // 'all', 'active', 'completed', 'cancelled'

  if (!currentUser) return <Navigate to="/" />;

  const isShopOwner = shopsData.some(s => s.ownerId === currentUser.id);

  const buyerHolds = holdRequests.filter(h => h.buyerId === currentUser.id);
  
  const sellerHolds = holdRequests.filter(h => {
    const product = productsData.find(p => p.id === h.productId);
    if (!product) return false;
    if (product.sellType === 'shop') {
      const shop = shopsData.find(s => s.id === product.shopId);
      return shop && shop.ownerId === currentUser.id;
    }
    return product.sellerId === currentUser.id;
  });

  // Merge them and remove duplicates just in case (e.g. buying from own shop)
  const allUserHolds = [...buyerHolds, ...sellerHolds].filter((hold, index, self) => 
    index === self.findIndex((t) => t.id === hold.id)
  );

  const isCancelled = (status) => status === 'Đã huỷ' || status === 'Khách huỷ' || status === 'Khách không tới';

  const displayHolds = allUserHolds.filter(h => {
    if (statusFilter === 'active') return h.status === 'Đang giữ';
    if (statusFilter === 'completed') return h.status === 'Đã nhận hàng';
    if (statusFilter === 'cancelled') return isCancelled(h.status);
    return true;
  });

  const renderHoldCard = (hold) => {
    const product = productsData.find(p => p.id === hold.productId);
    if (!product) return null;
    const shop = shopsData.find(s => s.id === product.shopId) || { name: 'Cá nhân' };
    const buyer = users.find(u => u.id === hold.buyerId);
    const isBuyer = hold.buyerId === currentUser.id;

    return (
      <div key={hold.id} className="bg-white border border-slate-200 rounded-2xl p-5 shadow-sm space-y-4">
        <div className="flex justify-between items-start border-b pb-4">
          <div className="flex gap-4">
            <img src={product.image} className="w-20 h-20 object-cover rounded-xl" alt={product.name} />
            <div>
              <h3 className="font-bold text-slate-800">{product.name}</h3>
              <p className="text-doosii-primary font-bold">{new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(product.price)}</p>
              {isBuyer ? (
                <p className="text-sm text-slate-500 mt-1 flex items-center gap-1"><Store className="w-4 h-4" /> {shop.name}</p>
              ) : (
                <p className="text-sm text-slate-500 mt-1 flex items-center gap-1">Khách: <span className="font-bold">{buyer?.fullname}</span></p>
              )}
            </div>
          </div>
          <div className={`px-3 py-1 text-sm font-bold rounded-lg ${
            hold.status === 'Đang giữ' ? 'bg-amber-100 text-amber-600' : 
            hold.status === 'Đã nhận hàng' ? 'bg-emerald-100 text-emerald-600' : 
            (hold.status === 'Khách huỷ' || hold.status === 'Đã huỷ' || hold.status === 'Khách không tới') ? 'bg-rose-100 text-rose-600' : 
            'bg-slate-100 text-slate-600'
          }`}>
            {hold.status === 'Đang giữ' ? 'Shop đang giữ' : hold.status}
          </div>
        </div>

        <div className="grid grid-cols-2 gap-4 text-sm bg-slate-50 p-4 rounded-xl">
          <div>
            <p className="text-slate-500 mb-1">Thời gian giữ</p>
            <p className="font-bold text-slate-800 flex items-center gap-1"><Clock className="w-4 h-4 text-amber-500" /> {hold.holdDuration}</p>
          </div>
          <div>
            <p className="text-slate-500 mb-1">Tiền cọc / Hoa hồng</p>
            <p className="font-bold text-slate-800">
              {new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(hold.depositAmount)} / {new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(hold.commissionAmount)}
            </p>
          </div>
        </div>

        {hold.status === 'Đang giữ' && (
          <div className="flex gap-3 pt-2">
            {isBuyer ? (
              <>
                <button onClick={() => updateHoldRequestStatus(hold.id, 'Khách huỷ')} className="flex-1 py-2.5 bg-slate-100 text-slate-600 font-bold rounded-xl hover:bg-rose-50 hover:text-rose-600 transition">Huỷ cọc</button>
                <button className="flex-1 py-2.5 bg-doosii-primary text-white font-bold rounded-xl opacity-50 cursor-not-allowed transition flex items-center justify-center gap-1">
                  Đến lấy tại Shop
                </button>
              </>
            ) : (
              <>
                <button onClick={() => updateHoldRequestStatus(hold.id, 'Khách không tới')} className="flex-1 py-2.5 bg-slate-100 text-slate-600 font-bold rounded-xl hover:bg-rose-50 hover:text-rose-600 transition">Khách không tới</button>
                <button onClick={() => updateHoldRequestStatus(hold.id, 'Đã nhận hàng')} className="flex-1 py-2.5 bg-emerald-500 text-white font-bold rounded-xl hover:bg-emerald-600 transition flex items-center justify-center gap-1">
                  <Check className="w-4 h-4" /> Xác nhận khách lấy
                </button>
              </>
            )}
          </div>
        )}
      </div>
    );
  };

  return (
    <div className="max-w-4xl mx-auto py-8 px-4">
      <h1 className="text-2xl font-black text-slate-800 mb-6 flex items-center gap-2">
        <ShieldCheck className="w-6 h-6 text-doosii-primary" /> Quản lý Giữ Đồ O2O
      </h1>

      {/* Status Filters */}
      <div className="flex flex-wrap gap-2 mb-6">
        {[
          { id: 'all', label: 'Tất cả' },
          { id: 'active', label: 'Shop đang giữ' },
          { id: 'completed', label: 'Đã nhận hàng' },
          { id: 'cancelled', label: 'Đã huỷ' }
        ].map(filter => (
          <button
            key={filter.id}
            onClick={() => setStatusFilter(filter.id)}
            className={`px-4 py-1.5 rounded-full text-sm font-bold transition ${
              statusFilter === filter.id 
                ? 'bg-doosii-primary text-white shadow-md' 
                : 'bg-white border border-slate-200 text-slate-600 hover:bg-slate-50'
            }`}
          >
            {filter.label}
          </button>
        ))}
      </div>

      {displayHolds.length === 0 ? (
        <div className="text-center py-20 bg-white rounded-3xl border border-slate-100 shadow-sm">
          <ShoppingBag className="w-16 h-16 mx-auto text-slate-300 mb-4" />
          <h2 className="text-xl font-bold text-slate-700 mb-2">Chưa có dữ liệu giữ đồ</h2>
          <p className="text-slate-500">Bạn chưa có yêu cầu giữ đồ nào trong mục này.</p>
        </div>
      ) : (
        <div className="space-y-4">
          {displayHolds.map(renderHoldCard)}
        </div>
      )}
    </div>
  );
};

export default HoldManagement;
