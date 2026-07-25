import React, { useState, useEffect, useRef } from 'react';
import { Navigate, Link, useLocation, useNavigate } from 'react-router-dom';
import useStore from '../store/useStore';
import { productsData, shopsData } from '../data/mockData';
import { 
  Search, Filter, Send, Image as ImageIcon, Zap, 
  ShieldCheck, AlertTriangle, MoreVertical, CreditCard,
  Tag, X, Check, ShoppingBag, Store, User
} from 'lucide-react';

const DepositCard = ({ msg, currentUserId, depositRequests, updateDepositStatus, setCheckoutProduct, navigate }) => {
  let contentData = { amount: 0 };
  try { contentData = JSON.parse(msg.content); } catch (e) {}

  const deposit = depositRequests.find(d => d.id === contentData.depositId);
  const status = deposit ? deposit.status : 'Unknown';

  return (
    <div className="bg-white border-2 border-indigo-500 rounded-xl p-4 w-64 md:w-72 shadow-md my-2 inline-block text-left">
      <div className="flex items-center gap-2 text-indigo-600 mb-2 font-bold border-b pb-2 border-indigo-100">
        <CreditCard className="w-5 h-5" /> Yêu cầu Trung gian (Cọc)
      </div>
      <div className="text-center my-3">
        <p className="text-xs text-slate-500 mb-1 uppercase font-bold tracking-wider">Số tiền cọc</p>
        <p className="text-2xl font-black text-slate-800">
          {new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(contentData.amount || 0)}
        </p>
      </div>
      
      {status === 'Pending' && msg.senderId !== currentUserId && (
        <div className="flex gap-2 mt-4">
          <button onClick={() => updateDepositStatus(deposit.id, 'Rejected')} className="flex-1 py-2 bg-slate-100 text-slate-600 font-bold rounded-lg hover:bg-slate-200 transition text-sm">Từ chối</button>
          <button onClick={() => updateDepositStatus(deposit.id, 'Accepted')} className="flex-1 py-2 bg-indigo-600 text-white font-bold rounded-lg hover:bg-indigo-700 transition text-sm">Đồng ý</button>
        </div>
      )}
      
      {status === 'Pending' && msg.senderId === currentUserId && (
        <div className="text-center text-sm font-medium text-orange-500 bg-orange-50 py-2 rounded-lg mt-3">Đang chờ đối phương xác nhận...</div>
      )}

      {status === 'Accepted' && (
        <div className="mt-3">
          <div className="text-center text-sm font-medium text-emerald-600 bg-emerald-50 py-2 rounded-lg flex items-center justify-center gap-1">
            <Check className="w-4 h-4" /> Đã xác nhận
          </div>
          <button 
            onClick={() => {
              const p = productsData.find(prod => prod.id === deposit.productId);
              if (p) {
                setCheckoutProduct(p);
                navigate('/checkout');
              }
            }} 
            className="w-full mt-2 py-2 bg-emerald-500 text-white font-bold rounded-lg hover:bg-emerald-600 transition text-sm flex items-center justify-center gap-1"
          >
            <ShoppingBag className="w-4 h-4" /> Thanh toán cọc ngay
          </button>
        </div>
      )}

      {status === 'Rejected' && (
        <div className="text-center text-sm font-medium text-red-600 bg-red-50 py-2 rounded-lg mt-3 flex items-center justify-center gap-1">
          <X className="w-4 h-4" /> Đã từ chối
        </div>
      )}
    </div>
  );
};

const ProductMessageCard = ({ productId }) => {
  const product = productsData.find(p => p.id === parseInt(productId));
  if (!product) return null;
  return (
    <div className="bg-white border border-slate-200 rounded-xl p-3 w-64 shadow-sm my-2 text-left">
      <img src={product.image} className="w-full h-32 object-cover rounded-lg mb-3" />
      <h4 className="font-bold text-slate-800 text-sm line-clamp-2">{product.name}</h4>
      <p className="font-black text-indigo-600 mt-1">{new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(product.price)}</p>
      <Link to={`/product/${product.id}`} className="block text-center mt-3 py-2 bg-indigo-50 text-indigo-600 font-bold rounded-lg text-xs hover:bg-indigo-100 transition">Xem chi tiết</Link>
    </div>
  );
};

const ChatArea = ({ activeConv }) => {
  const { currentUser, messages, users, sendMessage, depositRequests, updateDepositStatus, createDepositRequest, quickReplies, setCheckoutProduct } = useStore();
  const navigate = useNavigate();
  const [inputText, setInputText] = useState('');
  const [showQR, setShowQR] = useState(false);
  const [showDepositModal, setShowDepositModal] = useState(false);
  const [showSendProductModal, setShowSendProductModal] = useState(false);
  const [selectedSendProductId, setSelectedSendProductId] = useState('');
  const [depositType, setDepositType] = useState('amount'); // 'amount' | 'percent'
  const [depositAmount, setDepositAmount] = useState('');
  const [depositPercent, setDepositPercent] = useState(50);
  const [selectedProductId, setSelectedProductId] = useState(activeConv?.relatedProductId || '');
  const [depositRole, setDepositRole] = useState('seller');
  
  const endOfMessagesRef = useRef(null);
  
  const convMsgs = messages.filter(m => m.conversationId === activeConv?.id).sort((a,b) => new Date(a.createdAt) - new Date(b.createdAt));
  const otherUserId = activeConv?.participant1_Id === currentUser.id ? activeConv?.participant2_Id : activeConv?.participant1_Id;
  const otherUser = users.find(u => u.id === otherUserId);
  const product = productsData.find(p => p.id === activeConv?.relatedProductId);

  let isSeller = false;
  if (product) {
    if (product.sellType === 'shop') {
      const shop = shopsData.find(s => s.id === product.shopId);
      if (shop && shop.ownerId === currentUser.id) isSeller = true;
    } else {
      if (product.sellerId === currentUser.id) isSeller = true;
    }
  }

  const otherUserProducts = productsData.filter(p => {
    if (p.sellType === 'shop') {
      const shop = shopsData.find(s => s.id === p.shopId);
      return shop && shop.ownerId === otherUser?.id;
    }
    return false;
  });

  const canCreateDeposit = (isSeller && currentUser.isAbleToSell) || (product && product.sellType === 'pass') || (!product && (currentUser.isAbleToSell || otherUser?.isAbleToSell));

  useEffect(() => {
    endOfMessagesRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [convMsgs]);

  if (!activeConv) {
    return <div className="flex-1 flex items-center justify-center bg-slate-50 text-slate-400">Chọn một đoạn chat để bắt đầu</div>;
  }

  const handleSend = (e) => {
    e?.preventDefault();
    if (!inputText.trim()) return;
    sendMessage(activeConv.id, currentUser.id, 'Text', inputText.trim());
    setInputText('');
  };

  const handleCreateDeposit = () => {
    let finalAmount = 0;
    let finalPercent = null;
    const pId = parseInt(selectedProductId);
    const selectedProduct = productsData.find(p => p.id === pId);

    if (depositType === 'percent') {
       if (!selectedProduct) return alert('Vui lòng chọn sản phẩm để tính phần trăm');
       finalPercent = parseInt(depositPercent);
       finalAmount = Math.round(selectedProduct.price * (finalPercent / 100));
    } else {
       finalAmount = parseInt(depositAmount);
       if (!finalAmount) return alert('Vui lòng nhập số tiền hợp lệ');
    }

    const dep = createDepositRequest(activeConv.id, pId, finalAmount);
    sendMessage(activeConv.id, currentUser.id, 'DepositRequest', JSON.stringify({ amount: finalAmount, percentage: finalPercent, depositId: dep.id, productId: pId }));
    setShowDepositModal(false);
    setDepositAmount('');
  };

  const handleSendProduct = () => {
    if (!selectedSendProductId) return alert("Vui lòng chọn sản phẩm");
    sendMessage(activeConv.id, currentUser.id, 'ProductCard', selectedSendProductId);
    setShowSendProductModal(false);
  };

  return (
    <div className="flex-1 flex flex-col h-full bg-slate-50 relative min-w-0">
      {/* Header */}
      <div className="h-16 bg-white border-b border-slate-200 px-6 flex items-center justify-between shadow-sm z-10 shrink-0">
        <div className="flex items-center gap-3">
          <img src={otherUser?.avatar} alt="" className="w-10 h-10 rounded-full object-cover border" />
          <div>
            <h3 className="font-bold text-slate-800">{otherUser?.fullname || otherUser?.name}</h3>
            <p className="text-xs text-emerald-500 font-medium flex items-center gap-1"><span className="w-2 h-2 rounded-full bg-emerald-500 inline-block"></span> Đang hoạt động</p>
          </div>
        </div>
        <button className="text-slate-400 hover:text-slate-600"><MoreVertical className="w-5 h-5" /></button>
      </div>

      {/* Context Banner */}
      {product && (
        <div className="bg-indigo-50 border-b border-indigo-100 px-6 py-2 flex items-center justify-between shrink-0">
          <div className="flex items-center gap-3">
            <img src={product.image} className="w-10 h-10 rounded-md object-cover" />
            <div>
              <p className="text-sm font-bold text-slate-700 line-clamp-1">{product.name}</p>
              <p className="text-xs font-bold text-indigo-600">{new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(product.price)}</p>
            </div>
          </div>
          <Link to={`/product/${product.id}`} className="text-xs bg-white text-indigo-600 px-3 py-1.5 rounded border border-indigo-200 font-bold hover:bg-indigo-50 transition">Xem tin</Link>
        </div>
      )}

      {/* Messages */}
      <div className="flex-1 overflow-y-auto p-6 space-y-4">
        <div className="text-center text-xs text-slate-400 mb-6">Bắt đầu cuộc hội thoại an toàn qua DooSii Escrow</div>
        {convMsgs.map(msg => {
          const isMe = msg.senderId === currentUser.id;
          return (
            <div key={msg.id} className={`flex ${isMe ? 'justify-end' : 'justify-start'}`}>
              <div className={`max-w-[80%] ${isMe ? 'text-right' : 'text-left'}`}>
                {msg.messageType === 'DepositRequest' ? (
                  <DepositCard msg={msg} currentUserId={currentUser.id} depositRequests={depositRequests} updateDepositStatus={updateDepositStatus} setCheckoutProduct={setCheckoutProduct} navigate={navigate} />
                ) : msg.messageType === 'ProductCard' ? (
                  <ProductMessageCard productId={msg.content} />
                ) : (
                  <div className={`inline-block px-4 py-2.5 rounded-2xl ${isMe ? 'bg-indigo-600 text-white rounded-tr-sm' : 'bg-white border border-slate-200 text-slate-700 rounded-tl-sm shadow-sm'}`}>
                    <p className="text-[15px] leading-relaxed text-left">{msg.content}</p>
                  </div>
                )}
                <p className="text-[11px] text-slate-400 mt-1">{new Date(msg.createdAt).toLocaleTimeString([], {hour: '2-digit', minute:'2-digit'})}</p>
              </div>
            </div>
          );
        })}
        <div ref={endOfMessagesRef} />
      </div>

      {/* Input */}
      <div className="bg-white p-4 border-t border-slate-200 shrink-0">
        <form onSubmit={handleSend} className="flex items-end gap-2 relative">
          
          <button type="button" className="p-3 text-slate-400 hover:text-indigo-600 transition rounded-xl hover:bg-indigo-50"><ImageIcon className="w-5 h-5" /></button>
          
          {otherUserProducts.length > 0 && (
             <button type="button" onClick={() => setShowSendProductModal(true)} className="p-3 text-emerald-500 hover:text-emerald-600 transition rounded-xl hover:bg-emerald-50" title="Gửi sản phẩm"><ShoppingBag className="w-5 h-5" /></button>
          )}
          
          {isSeller && (
            <div className="relative">
              <button type="button" onClick={() => setShowQR(!showQR)} className="p-3 text-amber-500 hover:text-amber-600 transition rounded-xl hover:bg-amber-50">
                <Zap className="w-5 h-5 fill-current" />
              </button>
              {showQR && (
                <div className="absolute bottom-full left-0 mb-2 w-64 bg-white shadow-xl border border-slate-100 rounded-xl overflow-hidden z-20">
                  <div className="px-3 py-2 bg-slate-50 text-xs font-bold text-slate-500 border-b">TIN NHẮN NHANH</div>
                  {quickReplies.map(qr => (
                    <button key={qr.id} type="button" onClick={() => { setInputText(qr.content); setShowQR(false); }} className="w-full text-left px-4 py-3 hover:bg-indigo-50 text-sm text-slate-700 border-b last:border-0 transition line-clamp-2">
                      <span className="font-bold text-indigo-600 mr-2">{qr.shortcut}</span>{qr.content}
                    </button>
                  ))}
                </div>
              )}
            </div>
          )}

          <div className="flex-1 bg-slate-100 rounded-2xl border border-slate-200 focus-within:border-indigo-500 focus-within:ring-2 focus-within:ring-indigo-100 transition overflow-hidden">
            <input 
              type="text" 
              value={inputText}
              onChange={(e) => setInputText(e.target.value)}
              placeholder="Nhập tin nhắn..." 
              className="w-full bg-transparent px-4 py-3 outline-none text-sm"
            />
          </div>

          {canCreateDeposit && (
            <button type="button" onClick={() => setShowDepositModal(true)} className="px-4 py-3 bg-amber-500 hover:bg-amber-600 text-white font-bold rounded-2xl transition shadow-md whitespace-nowrap">
              Tạo Cọc
            </button>
          )}

          <button type="submit" disabled={!inputText.trim()} className="p-3 bg-indigo-600 text-white rounded-2xl hover:bg-indigo-700 disabled:opacity-50 disabled:bg-slate-300 transition shadow-md">
            <Send className="w-5 h-5" />
          </button>
        </form>
      </div>

      {/* Deposit Modal */}
      {showDepositModal && (
        <div className="absolute inset-0 bg-slate-900/40 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl w-full max-w-sm shadow-2xl overflow-hidden animate-in zoom-in-95">
            <div className="p-4 bg-amber-500 text-white font-bold flex justify-between items-center">
              Tạo cọc cho sản phẩm
              <button onClick={() => setShowDepositModal(false)}><X className="w-5 h-5" /></button>
            </div>
            <div className="p-6 space-y-5">
              
              {/* Chọn vai trò (Chỉ hiển thị khi giao dịch cá nhân hoặc không ghim sản phẩm) */}
              {(!product || product.sellType === 'pass') && (
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-2">Vai trò của bạn</label>
                  <div className="flex gap-2">
                    <label className="flex items-center gap-2 text-sm cursor-pointer">
                      <input type="radio" name="depositRole" value="buyer" checked={depositRole === 'buyer'} onChange={() => setDepositRole('buyer')} className="text-amber-500 focus:ring-amber-500" />
                      Người mua (Chủ động gửi cọc)
                    </label>
                    <label className="flex items-center gap-2 text-sm cursor-pointer ml-4">
                      <input type="radio" name="depositRole" value="seller" checked={depositRole === 'seller'} onChange={() => setDepositRole('seller')} className="text-amber-500 focus:ring-amber-500" />
                      Người bán (Yêu cầu khách cọc)
                    </label>
                  </div>
                </div>
              )}

              {/* Chọn sản phẩm */}
              <div>
                <label className="block text-sm font-bold text-slate-700 mb-2">Chọn sản phẩm</label>
                <select 
                  value={selectedProductId}
                  onChange={(e) => setSelectedProductId(e.target.value)}
                  className="w-full px-4 py-2.5 rounded-xl border border-slate-200 focus:ring-2 focus:ring-amber-500 outline-none font-medium text-sm"
                >
                  <option value="">-- Chọn sản phẩm --</option>
                  {productsData.filter(p => {
                    const targetOwnerId = (!product || product.sellType === 'pass') ? (depositRole === 'buyer' ? otherUserId : currentUser.id) : currentUser.id;
                    if (p.sellType === 'shop') {
                      const shop = shopsData.find(s => s.id === p.shopId);
                      return shop && shop.ownerId === targetOwnerId;
                    }
                    return p.sellerId === targetOwnerId;
                  }).map(p => (
                    <option key={p.id} value={p.id}>{p.name} - {new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(p.price)}</option>
                  ))}
                </select>
              </div>

              {/* Loại cọc */}
              <div>
                <label className="block text-sm font-bold text-slate-700 mb-2">Hình thức cọc</label>
                <div className="flex bg-slate-100 p-1 rounded-xl">
                  <button 
                    onClick={() => setDepositType('amount')} 
                    className={`flex-1 py-2 text-sm font-bold rounded-lg transition ${depositType === 'amount' ? 'bg-white text-amber-600 shadow-sm' : 'text-slate-500'}`}
                  >
                    Theo số tiền
                  </button>
                  <button 
                    onClick={() => setDepositType('percent')} 
                    className={`flex-1 py-2 text-sm font-bold rounded-lg transition ${depositType === 'percent' ? 'bg-white text-amber-600 shadow-sm' : 'text-slate-500'}`}
                  >
                    Theo % giá trị
                  </button>
                </div>
              </div>

              {/* Input Cọc */}
              {depositType === 'amount' ? (
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-1">Số tiền yêu cầu cọc (VNĐ)</label>
                  <input type="number" value={depositAmount} onChange={e => setDepositAmount(e.target.value)} placeholder="Ví dụ: 50000" className="w-full px-4 py-3 rounded-xl border border-slate-200 focus:ring-2 focus:ring-amber-500 outline-none font-bold text-lg text-amber-600" />
                </div>
              ) : (
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-1 flex justify-between">
                    <span>Mức phần trăm cọc</span>
                    <span className="text-amber-600 text-lg">{depositPercent}%</span>
                  </label>
                  <input 
                    type="range" 
                    min="1" max="100" step="1" 
                    value={depositPercent} 
                    onChange={e => setDepositPercent(e.target.value)} 
                    className="w-full accent-amber-500 mt-2"
                  />
                  {selectedProductId && (
                    <p className="text-right text-xs font-bold text-slate-500 mt-2">
                      Thành tiền: <span className="text-amber-600 text-sm">
                        {new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(Math.round((productsData.find(p => p.id === parseInt(selectedProductId))?.price || 0) * (depositPercent / 100)))}
                      </span>
                    </p>
                  )}
                </div>
              )}

              <p className="text-xs text-slate-500 text-center">Khách hàng sẽ nhận được thẻ yêu cầu thanh toán cọc này trực tiếp trong khung chat.</p>
              <button onClick={handleCreateDeposit} className="w-full py-3 bg-amber-500 text-white font-bold rounded-xl hover:bg-amber-600 shadow-md">Gửi Yêu cầu</button>
            </div>
          </div>
        </div>
      )}

      {/* Send Product Modal */}
      {showSendProductModal && (
        <div className="absolute inset-0 bg-slate-900/40 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl w-full max-w-sm shadow-2xl overflow-hidden animate-in zoom-in-95">
            <div className="p-4 bg-emerald-500 text-white font-bold flex justify-between items-center">
              Gửi sản phẩm đang quan tâm
              <button onClick={() => setShowSendProductModal(false)}><X className="w-5 h-5" /></button>
            </div>
            <div className="p-6 space-y-5">
              <div>
                <label className="block text-sm font-bold text-slate-700 mb-2">Chọn sản phẩm của người này</label>
                <select 
                  value={selectedSendProductId}
                  onChange={(e) => setSelectedSendProductId(e.target.value)}
                  className="w-full px-4 py-2.5 rounded-xl border border-slate-200 focus:ring-2 focus:ring-emerald-500 outline-none font-medium text-sm"
                >
                  <option value="">-- Chọn sản phẩm --</option>
                  {otherUserProducts.map(p => (
                    <option key={p.id} value={p.id}>{p.name} - {new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(p.price)}</option>
                  ))}
                </select>
              </div>
              <button onClick={handleSendProduct} className="w-full py-3 bg-emerald-500 text-white font-bold rounded-xl hover:bg-emerald-600 shadow-md">Gửi vào chat</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

const CasualChat = () => {
  const { currentUser, conversations, users } = useStore();
  const location = useLocation();
  const stateConvId = location.state?.activeConvId;
  
  // Lọc các conversation mà user này tham gia
  const myConvs = conversations.filter(c => c.participant1_Id === currentUser.id || c.participant2_Id === currentUser.id);
  const [activeConvId, setActiveConvId] = useState(stateConvId || (myConvs.length > 0 ? myConvs[0].id : null));
  const [searchQuery, setSearchQuery] = useState('');

  useEffect(() => {
    if (stateConvId) setActiveConvId(stateConvId);
  }, [stateConvId]);

  const activeConv = conversations.find(c => c.id === activeConvId);

  return (
    <div className="flex w-full h-full">
      {/* Sidebar */}
      <div className="w-80 bg-white border-r border-slate-200 flex flex-col shrink-0">
        <div className="p-4 border-b border-slate-100 space-y-4">
          <h2 className="text-xl font-bold text-slate-800">Tin nhắn</h2>
          <div className="relative">
            <Search className="w-4 h-4 absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
            <input 
              type="text" 
              placeholder="Tìm tên, email..." 
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="w-full bg-slate-100 pl-9 pr-4 py-2.5 rounded-xl text-sm outline-none focus:ring-2 focus:ring-indigo-100 border border-transparent focus:border-indigo-300" 
            />
          </div>
        </div>
        <div className="flex-1 overflow-y-auto p-2">
          {searchQuery ? (
            users.filter(u => 
              u.id !== currentUser.id && 
              (u.fullname?.toLowerCase().includes(searchQuery.toLowerCase()) || 
               u.email?.toLowerCase().includes(searchQuery.toLowerCase()))
            ).map(user => (
              <div key={user.id} className="flex items-center gap-3 p-3 rounded-xl hover:bg-slate-50 transition">
                <img src={user.avatar} className="w-12 h-12 rounded-full object-cover" />
                <div className="flex-1 min-w-0">
                  <h4 className="font-bold text-slate-800 truncate">{user.fullname}</h4>
                  <p className="text-xs text-slate-500 truncate mt-0.5">{user.email}</p>
                </div>
                <button 
                  onClick={() => {
                    const convId = useStore.getState().createConversation(currentUser.id, user.id, null);
                    setActiveConvId(convId);
                    setSearchQuery('');
                  }}
                  className="px-3 py-1.5 bg-indigo-100 text-indigo-600 font-bold text-xs rounded-lg hover:bg-indigo-200 transition shrink-0"
                >
                  Nhắn tin
                </button>
              </div>
            ))
          ) : (
            myConvs.map(conv => {
            const otherUser = users.find(u => u.id === (conv.participant1_Id === currentUser.id ? conv.participant2_Id : conv.participant1_Id));
            return (
              <div key={conv.id} onClick={() => setActiveConvId(conv.id)} className={`flex items-center gap-3 p-3 rounded-xl cursor-pointer transition ${activeConvId === conv.id ? 'bg-indigo-50' : 'hover:bg-slate-50'}`}>
                <img src={otherUser?.avatar} className="w-12 h-12 rounded-full object-cover" />
                <div className="flex-1 min-w-0">
                  <h4 className="font-bold text-slate-800 truncate">{otherUser?.fullname || otherUser?.name}</h4>
                  <p className="text-xs text-slate-500 truncate mt-0.5">Đã xem tin nhắn cuối...</p>
                </div>
              </div>
            )
          }))}
        </div>
      </div>
      
      <ChatArea activeConv={activeConv} />
    </div>
  );
};

const ShopperChat = () => {
  const { currentUser, conversations, users, shopTags, conversationTags } = useStore();
  const location = useLocation();
  const stateConvId = location.state?.activeConvId;
  
  const myConvs = conversations.filter(c => c.participant1_Id === currentUser.id || c.participant2_Id === currentUser.id);
  const [activeConvId, setActiveConvId] = useState(stateConvId || (myConvs.length > 0 ? myConvs[0].id : null));
  const [searchQuery, setSearchQuery] = useState('');

  useEffect(() => {
    if (stateConvId) setActiveConvId(stateConvId);
  }, [stateConvId]);

  const activeConv = conversations.find(c => c.id === activeConvId);
  const otherUserId = activeConv?.participant1_Id === currentUser.id ? activeConv?.participant2_Id : activeConv?.participant1_Id;
  const otherUser = users.find(u => u.id === otherUserId);

  const currentTags = activeConvId ? conversationTags.filter(ct => ct.conversationId === activeConvId).map(ct => shopTags.find(t => t.id === ct.tagId)) : [];

  return (
    <div className="flex w-full h-full">
      {/* Sidebar Advanced */}
      <div className="w-80 bg-slate-50 border-r border-slate-200 flex flex-col shrink-0">
        <div className="p-4 border-b border-slate-200 bg-white">
          <div className="flex justify-between items-center mb-4">
            <h2 className="text-xl font-bold text-slate-800 flex items-center gap-2"><Store className="w-5 h-5 text-indigo-600" /> Hộp thư Shop</h2>
            <button className="p-2 text-slate-400 hover:bg-slate-100 rounded-lg"><Filter className="w-4 h-4" /></button>
          </div>
          <div className="relative">
            <Search className="w-4 h-4 absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
            <input 
              type="text" 
              placeholder="Tìm user bằng tên, email..." 
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="w-full bg-slate-100 pl-9 pr-4 py-2.5 rounded-xl text-sm outline-none focus:ring-2 focus:ring-indigo-100 border border-transparent focus:border-indigo-300" 
            />
          </div>
          <div className="flex gap-2 mt-3 overflow-x-auto pb-1 no-scrollbar">
            <span className="text-[10px] font-bold uppercase tracking-wider px-3 py-1 bg-white border border-slate-200 rounded-full text-slate-600 whitespace-nowrap shadow-sm">Tất cả</span>
            <span className="text-[10px] font-bold uppercase tracking-wider px-3 py-1 bg-red-50 border border-red-200 rounded-full text-red-600 whitespace-nowrap shadow-sm">Chưa đọc (2)</span>
            <span className="text-[10px] font-bold uppercase tracking-wider px-3 py-1 bg-amber-50 border border-amber-200 rounded-full text-amber-600 whitespace-nowrap shadow-sm">Chờ cọc (1)</span>
          </div>
        </div>
        
        <div className="flex-1 overflow-y-auto p-2 bg-white">
          {searchQuery ? (
            users.filter(u => 
              u.id !== currentUser.id && 
              (u.fullname?.toLowerCase().includes(searchQuery.toLowerCase()) || 
               u.email?.toLowerCase().includes(searchQuery.toLowerCase()))
            ).map(user => (
              <div key={user.id} className="flex items-center gap-3 p-3 rounded-xl hover:bg-slate-50 transition border-b border-slate-100">
                <img src={user.avatar} className="w-12 h-12 rounded-full object-cover" />
                <div className="flex-1 min-w-0">
                  <h4 className="font-bold text-slate-800 truncate">{user.fullname}</h4>
                  <p className="text-xs text-slate-500 truncate mt-0.5">{user.email}</p>
                </div>
                <button 
                  onClick={() => {
                    const convId = useStore.getState().createConversation(currentUser.id, user.id, null);
                    setActiveConvId(convId);
                    setSearchQuery('');
                  }}
                  className="px-3 py-1.5 bg-indigo-100 text-indigo-600 font-bold text-xs rounded-lg hover:bg-indigo-200 transition shrink-0"
                >
                  Nhắn tin
                </button>
              </div>
            ))
          ) : (
            myConvs.map(conv => {
              const user = users.find(u => u.id === (conv.participant1_Id === currentUser.id ? conv.participant2_Id : conv.participant1_Id));
              const tags = conversationTags.filter(ct => ct.conversationId === conv.id).map(ct => shopTags.find(t => t.id === ct.tagId));
            
            return (
              <div key={conv.id} onClick={() => setActiveConvId(conv.id)} className={`p-3 rounded-xl cursor-pointer border-l-4 transition mb-2 ${activeConvId === conv.id ? 'border-indigo-600 bg-indigo-50/50 shadow-sm' : 'border-transparent hover:bg-slate-50'}`}>
                <div className="flex items-center gap-3 mb-2">
                  <img src={user?.avatar} className="w-10 h-10 rounded-full object-cover" />
                  <div className="flex-1 min-w-0">
                    <h4 className="font-bold text-slate-800 text-sm truncate">{user?.fullname}</h4>
                    <p className="text-xs text-slate-500 truncate mt-0.5">Khách hỏi size quần Levi's...</p>
                  </div>
                </div>
                <div className="flex gap-1 pl-13">
                  {tags.map(t => t && (
                    <span key={t.id} className="text-[9px] font-bold px-1.5 py-0.5 rounded" style={{ backgroundColor: t.colorCode + '20', color: t.colorCode }}>
                      {t.tagName}
                    </span>
                  ))}
                </div>
              </div>
            )
          }))}
        </div>
      </div>
      
      {/* Middle Chat Area */}
      <ChatArea activeConv={activeConv} />

      {/* Right CRM Panel */}
      <div className="w-72 bg-white border-l border-slate-200 flex flex-col shrink-0">
        <div className="p-4 border-b border-slate-100 flex justify-center flex-col items-center pt-8 bg-slate-50">
          <div className="relative">
            <img src={otherUser?.avatar} className="w-20 h-20 rounded-full object-cover border-4 border-white shadow-md mb-3" />
            <span className="absolute bottom-4 right-0 w-4 h-4 bg-emerald-500 border-2 border-white rounded-full"></span>
          </div>
          <h3 className="font-black text-lg text-slate-800">{otherUser?.fullname}</h3>
          <p className="text-sm text-slate-500 mb-4">{otherUser?.role}</p>
          <div className="flex gap-2 w-full">
            <button className="flex-1 bg-white border border-slate-200 text-slate-700 py-2 rounded-lg text-sm font-bold hover:bg-slate-50 shadow-sm">Trang cá nhân</button>
          </div>
        </div>

        <div className="p-5 flex-1 overflow-y-auto space-y-6">
          
          <div>
            <h4 className="text-xs font-bold text-slate-400 uppercase tracking-wider mb-3">Nhãn phân loại</h4>
            <div className="flex flex-wrap gap-2">
              {currentTags.map(t => t && (
                <span key={t.id} className="text-xs font-bold px-2.5 py-1 rounded-md border flex items-center gap-1 shadow-sm" style={{ backgroundColor: t.colorCode + '10', borderColor: t.colorCode + '30', color: t.colorCode }}>
                  <Tag className="w-3 h-3" /> {t.tagName}
                </span>
              ))}
              <button className="text-xs font-bold px-2.5 py-1 rounded-md border border-dashed border-slate-300 text-slate-500 hover:bg-slate-50">+ Thêm</button>
            </div>
          </div>

          <div className="bg-white rounded-xl p-4 border border-slate-100 shadow-sm">
            <h4 className="text-xs font-bold text-slate-500 uppercase tracking-wider mb-4 flex items-center gap-2"><ShieldCheck className="w-4 h-4 text-emerald-500" /> Chỉ số Uy tín (Trust)</h4>
            <div className="flex justify-between items-end mb-2">
              <span className="text-3xl font-black text-slate-800">95<span className="text-sm text-slate-400">/100</span></span>
              <span className="text-xs font-bold text-emerald-600 bg-emerald-100 px-2 py-1 rounded">Rất tốt</span>
            </div>
            <div className="w-full bg-slate-200 rounded-full h-1.5 mb-4"><div className="bg-emerald-500 h-1.5 rounded-full w-[95%]"></div></div>
            <ul className="text-xs text-slate-600 space-y-2">
              <li className="flex justify-between"><span>Đã nhận hàng:</span> <span className="font-bold">12 đơn</span></li>
              <li className="flex justify-between"><span>Tỷ lệ hoàn trả (Bom):</span> <span className="font-bold text-red-500">0%</span></li>
            </ul>
          </div>

          <div>
            <h4 className="text-xs font-bold text-slate-400 uppercase tracking-wider mb-3">Lịch sử mua hàng</h4>
            <div className="text-sm text-slate-500 text-center py-4 bg-slate-50 rounded-xl border border-dashed border-slate-200">
              Khách chưa mua sản phẩm nào của Shop.
            </div>
          </div>
          
        </div>
      </div>
    </div>
  );
};

const Chat = () => {
  const { currentUser } = useStore();
  if (!currentUser) return <Navigate to="/login" replace />;

  return (
    <div className="h-[calc(100vh-140px)] animate-in fade-in flex bg-white rounded-3xl shadow-2xl overflow-hidden border border-slate-200">
      {currentUser.role === 'Shopper' ? <ShopperChat /> : <CasualChat />}
    </div>
  );
};

export default Chat;
