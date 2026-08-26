import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { ShieldCheck, Truck, CreditCard, Wallet } from 'lucide-react';
import useStore from '../store/useStore';

const Checkout = () => {
  const { checkoutProduct, setCheckoutProduct, createOrder, currentUser, depositRequests, conversations } = useStore();
  const navigate = useNavigate();
  const [paymentMethod, setPaymentMethod] = useState('escrow_full');
  const [paymentType, setPaymentType] = useState('vnpay');

  useEffect(() => {
    if (!checkoutProduct) {
      navigate('/');
    }
  }, [checkoutProduct, navigate]);

  if (!checkoutProduct) return null;

  const subtotal = checkoutProduct.price;
  const shipping = 30000;
  const commission = subtotal * 0.03;
  const total = subtotal + shipping + commission;

  // Find accepted deposit for this product and current user
  const validDeposit = depositRequests.find(d => 
    d.productId == checkoutProduct.id && 
    d.status === 'Accepted' && 
    conversations.find(c => c.id == d.conversationId && (c.participant1_Id == currentUser?.id || c.participant2_Id == currentUser?.id))
  );

  const depositUsed = paymentMethod === 'escrow_deposit' ? validDeposit?.requestedAmount || 0 : 0;
  const remainingAmount = total - depositUsed;

  const handleCheckout = (e) => {
    e.preventDefault();
    if (!currentUser) {
      alert("Vui lòng 'Mock Login' ở thanh Navbar để tiến hành thanh toán!");
      return;
    }

    const orderData = {
      buyerId: currentUser.id,
      items: [checkoutProduct],
      subtotal,
      commission,
      shipping,
      total,
      paymentMethod,
      paymentType,
      paymentMethodDelivery: paymentMethod === 'escrow_deposit' ? 'cash' : null,
      depositAmount: depositUsed,
      status: 'Đã đặt',
      date: new Date().toISOString()
    };

    createOrder(orderData);
    setCheckoutProduct(null);
    alert("Đơn hàng đã được tạo thành công");
    navigate('/dashboard');
  };

  return (
    <div className="max-w-6xl mx-auto grid grid-cols-1 md:grid-cols-2 gap-8 animate-in fade-in">
      <div className="space-y-6">
        <h2 className="text-2xl font-bold text-slate-800">Thanh toán an toàn</h2>
        
        <form id="checkout-form" onSubmit={handleCheckout} className="space-y-6">
          <div className="glass-card p-6 space-y-4">
            <h3 className="font-bold flex items-center gap-2 text-slate-700">
              <Truck className="w-5 h-5" /> Thông tin giao hàng
            </h3>
            <div className="space-y-3">
              <input required type="text" placeholder="Họ và tên" defaultValue={currentUser?.fullname} className="w-full px-4 py-3 rounded-xl border border-slate-200 focus:ring-2 focus:ring-doosii-primary focus:outline-none" />
              <input required type="tel" placeholder="Số điện thoại" className="w-full px-4 py-3 rounded-xl border border-slate-200 focus:ring-2 focus:ring-doosii-primary focus:outline-none" />
              <textarea required placeholder="Địa chỉ giao hàng (Ví dụ: 123 Võ Văn Ngân, Thủ Đức...)" rows="3" className="w-full px-4 py-3 rounded-xl border border-slate-200 focus:ring-2 focus:ring-doosii-primary focus:outline-none"></textarea>
            </div>
          </div>

          <div className="glass-card p-6 space-y-4">
            <h3 className="font-bold flex items-center gap-2 text-slate-700">
              <Wallet className="w-5 h-5" /> Phương thức Thanh toán
            </h3>
            
            <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
              <label className={`flex items-center justify-center p-3 border rounded-xl cursor-pointer transition ${paymentType === 'cash' ? 'border-doosii-primary bg-doosii-primary/5 text-doosii-primary font-bold' : 'border-slate-200 text-slate-600 hover:bg-slate-50'}`}>
                <input type="radio" name="paymentType" value="cash" checked={paymentType === 'cash'} onChange={() => setPaymentType('cash')} className="hidden" />
                Tiền mặt
              </label>
              <label className={`flex items-center justify-center p-3 border rounded-xl cursor-pointer transition ${paymentType === 'vnpay' ? 'border-doosii-primary bg-doosii-primary/5 text-doosii-primary font-bold' : 'border-slate-200 text-slate-600 hover:bg-slate-50'}`}>
                <input type="radio" name="paymentType" value="vnpay" checked={paymentType === 'vnpay'} onChange={() => setPaymentType('vnpay')} className="hidden" />
                VNPAY
              </label>
              <label className={`flex items-center justify-center p-3 border rounded-xl cursor-pointer transition ${paymentType === 'visa' ? 'border-doosii-primary bg-doosii-primary/5 text-doosii-primary font-bold text-center' : 'border-slate-200 text-slate-600 hover:bg-slate-50 text-center text-sm'}`}>
                <input type="radio" name="paymentType" value="visa" checked={paymentType === 'visa'} onChange={() => setPaymentType('visa')} className="hidden" />
                Visa/Master
              </label>
              <label className={`flex items-center justify-center p-3 border rounded-xl cursor-pointer transition ${paymentType === 'napas' ? 'border-doosii-primary bg-doosii-primary/5 text-doosii-primary font-bold' : 'border-slate-200 text-slate-600 hover:bg-slate-50'}`}>
                <input type="radio" name="paymentType" value="napas" checked={paymentType === 'napas'} onChange={() => setPaymentType('napas')} className="hidden" />
                Napas
              </label>
            </div>

            {(paymentType === 'visa' || paymentType === 'napas') && (
              <div className="mt-4 p-4 bg-slate-50 border border-slate-100 rounded-xl space-y-3 animate-in slide-in-from-top-2">
                <input required type="text" placeholder="Số thẻ" className="w-full px-4 py-2.5 rounded-lg border border-slate-200 focus:ring-2 focus:ring-doosii-primary focus:outline-none text-sm" />
                <input required type="text" placeholder="Tên in trên thẻ (Không dấu)" className="w-full px-4 py-2.5 rounded-lg border border-slate-200 focus:ring-2 focus:ring-doosii-primary focus:outline-none text-sm uppercase" />
                <div className="grid grid-cols-2 gap-3">
                  <input required type="text" placeholder="MM/YY" className="w-full px-4 py-2.5 rounded-lg border border-slate-200 focus:ring-2 focus:ring-doosii-primary focus:outline-none text-sm" />
                  <input required type="password" placeholder="CVV" maxLength="4" className="w-full px-4 py-2.5 rounded-lg border border-slate-200 focus:ring-2 focus:ring-doosii-primary focus:outline-none text-sm" />
                </div>
              </div>
            )}
          </div>

          <div className="glass-card p-6 space-y-4">
            <h3 className="font-bold flex items-center gap-2 text-slate-700">
              <CreditCard className="w-5 h-5" /> Phương thức Trung gian (Escrow)
            </h3>
            <p className="text-xs text-slate-500 mb-4 bg-blue-50 p-3 rounded-lg border border-blue-100">
              <ShieldCheck className="w-4 h-4 inline mr-1 text-blue-500" />
              Tiền của bạn sẽ được DooSii giữ an toàn. Chỉ chuyển cho người bán khi bạn xác nhận nhận hàng và đồng kiểm thành công.
            </p>
            
            <div className="space-y-3">
              <label className={`flex items-center gap-3 p-4 border rounded-xl cursor-pointer transition ${paymentMethod === 'escrow_full' ? 'border-doosii-primary bg-doosii-primary/5' : 'border-slate-200'}`}>
                <input type="radio" name="payment" value="escrow_full" checked={paymentMethod === 'escrow_full'} onChange={() => setPaymentMethod('escrow_full')} className="w-5 h-5 text-doosii-primary" />
                <div>
                  <p className="font-bold text-slate-800">Thanh toán toàn bộ</p>
                  <p className="text-sm text-slate-500">Chuyển khoản VNPAY/Napas/Visa cho hệ thống 100%</p>
                </div>
              </label>

              <label className={`flex items-center gap-3 p-4 border rounded-xl transition ${validDeposit ? 'cursor-pointer hover:bg-slate-50' : 'opacity-50 cursor-not-allowed'} ${paymentMethod === 'escrow_deposit' ? 'border-doosii-primary bg-doosii-primary/5' : 'border-slate-200'}`}>
                <input 
                  type="radio" 
                  name="payment" 
                  value="escrow_deposit" 
                  checked={paymentMethod === 'escrow_deposit'} 
                  onChange={() => setPaymentMethod('escrow_deposit')} 
                  disabled={!validDeposit}
                  className="w-5 h-5 text-doosii-primary" 
                />
                <div className="flex-1">
                  <p className="font-bold text-slate-800">Cọc trước một phần</p>
                  {validDeposit ? (
                    <div>
                      <p className="text-sm text-slate-500">
                        Đã có thoả thuận cọc: <span className="font-bold text-amber-600">{new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(validDeposit.requestedAmount)}</span>.
                      </p>
                      <p className="text-sm text-slate-500 mt-1 font-medium italic">
                        *Lưu ý: Phần còn lại thanh toán bằng Tiền mặt (COD) khi nhận hàng.
                      </p>
                    </div>
                  ) : (
                    <p className="text-sm text-red-500 italic">Bạn chưa có thoả thuận cọc nào cho sản phẩm này từ người bán.</p>
                  )}
                </div>
              </label>
            </div>
          </div>
        </form>
      </div>

      <div className="space-y-6">
        <div className="glass-card p-6 space-y-4 sticky top-24">
          <h3 className="font-bold text-xl text-slate-800 border-b pb-4">Đơn hàng của bạn</h3>
          
          <div className="space-y-4 pr-2">
            <div className="flex justify-between items-center text-sm">
              <span className="text-slate-600 line-clamp-2 flex-[2] pr-4">{checkoutProduct.name}</span>
              <span className="font-bold text-slate-800 flex-1 text-right">
                {new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(checkoutProduct.price)}
              </span>
            </div>
          </div>

          <div className="border-t pt-4 space-y-3 text-sm">
            <div className="flex justify-between">
              <span className="text-slate-500">Tạm tính:</span>
              <span className="font-semibold text-slate-800">
                {new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(subtotal)}
              </span>
            </div>
            <div className="flex justify-between">
              <span className="text-slate-500">Phí vận chuyển:</span>
              <span className="font-semibold text-slate-800">
                {new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(shipping)}
              </span>
            </div>
            <div className="flex justify-between">
              <span className="text-slate-500">Phí hoa hồng nền tảng (3%):</span>
              <span className="font-semibold text-slate-800">
                {new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(commission)}
              </span>
            </div>
            
            <div className="flex justify-between pt-4 border-t">
              <span className="font-bold text-lg text-slate-800">Tổng cộng:</span>
              <span className="font-black text-xl text-slate-800">
                {new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(total)}
              </span>
            </div>

            {paymentMethod === 'escrow_deposit' && validDeposit && (
              <div className="pt-4 border-t space-y-2 bg-amber-50 p-3 rounded-lg border border-amber-100 mt-2">
                <div className="flex justify-between text-amber-700">
                  <span className="font-bold text-sm">Cần thanh toán cọc ngay:</span>
                  <span className="font-black">
                    {new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(depositUsed)}
                  </span>
                </div>
                <div className="flex justify-between text-slate-600 text-sm">
                  <span>Số tiền trả khi nhận hàng:</span>
                  <span className="font-bold">
                    {new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(remainingAmount)}
                  </span>
                </div>
              </div>
            )}
            
            {paymentMethod !== 'escrow_deposit' && (
              <div className="flex justify-between pt-2">
                <span className="font-bold text-lg text-doosii-primary">Cần thanh toán ngay:</span>
                <span className="font-black text-2xl text-doosii-primary">
                  {new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(total)}
                </span>
              </div>
            )}
          </div>

          <button 
            type="submit"
            form="checkout-form"
            className="w-full mt-6 bg-doosii-primary text-white py-4 rounded-xl font-bold text-lg hover:bg-doosii-primary/90 transition shadow-lg shadow-doosii-primary/30"
          >
            Đặt hàng
          </button>
        </div>
      </div>
    </div>
  );
};

export default Checkout;
