import React, { useState } from 'react';
import { X, Clock, CreditCard, ShieldCheck, Store } from 'lucide-react';
import useStore from '../store/useStore';

const HoldItemModal = ({ product, shop, onClose }) => {
  const { currentUser, createHoldRequest } = useStore();
  const [holdTime, setHoldTime] = useState('1h');
  const [paymentType, setPaymentType] = useState('vnpay');

  const holdOptions = [
    { value: '1h', label: '1 Giờ', depositPercent: 0 },
    { value: '2h', label: '2 Giờ', depositPercent: 0 },
    { value: '12h', label: '12 Giờ', depositPercent: 3 },
    { value: '24h', label: '24 Giờ', depositPercent: 10 },
    { value: '48h', label: '48 Giờ', depositPercent: 30 },
    { value: '1w', label: '1 Tuần', depositPercent: 60 }
  ];

  const selectedOption = holdOptions.find(o => o.value === holdTime);
  const depositAmount = Math.round(product.price * (selectedOption.depositPercent / 100));
  const commissionAmount = selectedOption.depositPercent > 0 ? Math.round(product.price * 0.03) : 0;
  const totalAmount = depositAmount + commissionAmount;

  const handleConfirm = (e) => {
    e.preventDefault();
    if (!currentUser) return alert('Vui lòng đăng nhập!');
    
    // Auto-approve if it's free
    createHoldRequest(
      currentUser.id,
      product.id,
      holdTime,
      depositAmount,
      commissionAmount,
      totalAmount > 0 ? paymentType : 'free'
    );

    alert('Đã tạo yêu cầu giữ đồ thành công!');
    onClose();
  };

  return (
    <div className="fixed inset-0 bg-slate-900/50 backdrop-blur-sm z-[9999] flex items-center justify-center p-4">
      <div className="bg-white rounded-2xl w-full max-w-lg shadow-2xl overflow-hidden animate-in zoom-in-95 max-h-[90vh] overflow-y-auto">
        <div className="p-4 bg-doosii-primary text-white font-bold flex justify-between items-center sticky top-0 z-10">
          Giữ đồ trước - In-store Hold
          <button onClick={onClose} className="hover:bg-white/20 p-1 rounded transition"><X className="w-5 h-5" /></button>
        </div>
        
        <form onSubmit={handleConfirm} className="p-6 space-y-6">
          {/* Product Info */}
          <div className="flex gap-4 p-4 bg-slate-50 rounded-xl border border-slate-100">
            <img src={product.image} className="w-20 h-20 object-cover rounded-lg" alt={product.name} />
            <div>
              <h3 className="font-bold text-slate-800 line-clamp-1">{product.name}</h3>
              <p className="text-doosii-primary font-bold">{new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(product.price)}</p>
              <p className="text-sm text-slate-500 mt-1 flex items-center gap-1"><Store className="w-4 h-4" /> {shop.name}</p>
            </div>
          </div>

          {/* Hold Time Selection */}
          <div>
            <label className="block text-sm font-bold text-slate-700 mb-3 flex items-center gap-2">
              <Clock className="w-4 h-4 text-doosii-primary" /> Chọn thời gian giữ đồ
            </label>
            <div className="grid grid-cols-3 gap-3">
              {holdOptions.map(opt => (
                <button
                  key={opt.value}
                  type="button"
                  onClick={() => setHoldTime(opt.value)}
                  className={`py-2 px-3 border rounded-xl text-sm font-medium transition ${
                    holdTime === opt.value 
                      ? 'border-doosii-primary bg-doosii-primary/10 text-doosii-primary shadow-sm' 
                      : 'border-slate-200 text-slate-600 hover:bg-slate-50'
                  }`}
                >
                  {opt.label}
                </button>
              ))}
            </div>
          </div>

          {/* Cost Summary */}
          <div className="bg-amber-50 p-4 rounded-xl border border-amber-100 space-y-2">
            <div className="flex justify-between text-sm">
              <span className="text-slate-600">Tiền cọc ({selectedOption.depositPercent}%):</span>
              <span className="font-medium text-slate-800">{new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(depositAmount)}</span>
            </div>
            {commissionAmount > 0 && (
              <div className="flex justify-between text-sm">
                <span className="text-slate-600">Phí hoa hồng (3%):</span>
                <span className="font-medium text-slate-800">{new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(commissionAmount)}</span>
              </div>
            )}
            <div className="pt-2 border-t border-amber-200 flex justify-between font-bold text-lg">
              <span className="text-slate-800">Tổng thanh toán:</span>
              <span className="text-doosii-primary">{new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(totalAmount)}</span>
            </div>
          </div>

          {/* Payment Method */}
          {totalAmount > 0 && (
            <div className="space-y-4">
              <label className="block text-sm font-bold text-slate-700 flex items-center gap-2">
                <CreditCard className="w-4 h-4 text-doosii-primary" /> Phương thức thanh toán cọc
              </label>
              
              <div className="grid grid-cols-3 gap-3">
                <label className={`flex items-center justify-center p-3 border rounded-xl cursor-pointer transition ${paymentType === 'vnpay' ? 'border-doosii-primary bg-doosii-primary/5 text-doosii-primary font-bold' : 'border-slate-200 text-slate-600 hover:bg-slate-50 text-sm'}`}>
                  <input type="radio" name="paymentType" value="vnpay" checked={paymentType === 'vnpay'} onChange={() => setPaymentType('vnpay')} className="hidden" />
                  VNPAY
                </label>
                <label className={`flex items-center justify-center p-3 border rounded-xl cursor-pointer transition ${paymentType === 'visa' ? 'border-doosii-primary bg-doosii-primary/5 text-doosii-primary font-bold' : 'border-slate-200 text-slate-600 hover:bg-slate-50 text-sm'}`}>
                  <input type="radio" name="paymentType" value="visa" checked={paymentType === 'visa'} onChange={() => setPaymentType('visa')} className="hidden" />
                  Visa/Master
                </label>
                <label className={`flex items-center justify-center p-3 border rounded-xl cursor-pointer transition ${paymentType === 'napas' ? 'border-doosii-primary bg-doosii-primary/5 text-doosii-primary font-bold' : 'border-slate-200 text-slate-600 hover:bg-slate-50 text-sm'}`}>
                  <input type="radio" name="paymentType" value="napas" checked={paymentType === 'napas'} onChange={() => setPaymentType('napas')} className="hidden" />
                  Napas
                </label>
              </div>

              {(paymentType === 'visa' || paymentType === 'napas') && (
                <div className="mt-3 p-4 bg-slate-50 border border-slate-100 rounded-xl space-y-3 animate-in slide-in-from-top-2">
                  <input required type="text" placeholder="Số thẻ" className="w-full px-4 py-2 rounded-lg border border-slate-200 focus:ring-2 focus:ring-doosii-primary focus:outline-none text-sm" />
                  <input required type="text" placeholder="Tên in trên thẻ (Không dấu)" className="w-full px-4 py-2 rounded-lg border border-slate-200 focus:ring-2 focus:ring-doosii-primary focus:outline-none text-sm uppercase" />
                  <div className="grid grid-cols-2 gap-3">
                    <input required type="text" placeholder="MM/YY" className="w-full px-4 py-2 rounded-lg border border-slate-200 focus:ring-2 focus:ring-doosii-primary focus:outline-none text-sm" />
                    <input required type="password" placeholder="CVV" maxLength="4" className="w-full px-4 py-2 rounded-lg border border-slate-200 focus:ring-2 focus:ring-doosii-primary focus:outline-none text-sm" />
                  </div>
                </div>
              )}
            </div>
          )}

          {/* Warning */}
          <div className="bg-blue-50 text-blue-600 p-3 rounded-lg text-xs flex gap-2 items-start">
            <ShieldCheck className="w-4 h-4 shrink-0 mt-0.5" />
            <p>Sau khi xác nhận, sản phẩm sẽ được giữ cho bạn tại cửa hàng. Vui lòng đến thử và lấy hàng trước khi thời gian kết thúc.</p>
          </div>

          <button type="submit" className="w-full bg-doosii-primary text-white font-bold py-3.5 rounded-xl hover:bg-indigo-700 transition shadow-lg flex justify-center items-center gap-2">
            Xác nhận giữ đồ {totalAmount > 0 ? `(${new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(totalAmount)})` : '(Miễn phí)'}
          </button>
        </form>
      </div>
    </div>
  );
};

export default HoldItemModal;
