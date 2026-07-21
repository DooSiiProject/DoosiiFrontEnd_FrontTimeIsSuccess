export const usersData = [
  {
    id: 1,
    fullname: "Anya Forger",
    email: "AnyaWithThinkingSenne@gmail.com",
    password: "Chunnimommy",
    cccd: null,
    mattruocCCCD: null,
    matsauCCCD: null,
    isAbleToSell: false,
    status: "active",
    avatar: "/AnyaForgerAvatar.jpg",
    role: "CasualUser"
  },
  {
    id: 2,
    fullname: "Loid Forger",
    email: "Loidthichloichoi@gmail.com",
    password: "LoidisLoid",
    cccd: "079090123456",
    mattruocCCCD: "mock_url_front",
    matsauCCCD: "mock_url_back",
    isAbleToSell: true,
    status: "active",
    avatar: "/LoidForgerUser.jpg",
    role: "CasualSeller"
  },
  {
    id: 3,
    fullname: "Yor Forger",
    email: "YorGoldForGirlForMyForgerFamilu@gmail.com",
    password: "YorGirlShopshoping97",
    cccd: "079090654321",
    mattruocCCCD: "mock_url_front",
    matsauCCCD: "mock_url_back",
    isAbleToSell: true,
    status: "active",
    avatar: "/YorForgerUser.jpg",
    role: "Shopper"
  },
  {
    id: 4,
    fullname: "Bond Forger",
    email: "MailMailGoGo@gmail.com",
    password: "Letgogo11",
    cccd: "079090999999",
    mattruocCCCD: "mock_url_front",
    matsauCCCD: "mock_url_back",
    isAbleToSell: false,
    status: "active",
    avatar: "/BonForgerUser.jpg",
    role: "Admin"
  }
];

export const shopsData = [
  {
    id: "SHOP_01",
    name: "For Yor Fashion",
    lat: 10.8490, 
    lng: 106.7530,
    establishedDate: "2025-09-09",
    status: "active",
    logoAvatar: "/YorForgerUser.jpg",
    ownerId: 3
  }
];

const baseProducts = [
  {
    id: 1,
    name: "Áo khoác da lộn lót lông",
    sellType: "shop",
    sellerId: null,
    shopId: "SHOP_01",
    price: 550000,
    category: "Áo khoác",
    specs: { daiAo: 68, ngangVai: 50, ngangThan: 56, daiTay: 62 },
    description: "Áo da lộn lót lông cừu ấm áp, độ mới 95%.",
    recommend: { height: "170-180 cm", weight: "65-75 kg" },
    brand: "Vintage",
    origin: "Nhật Bản",
    colors: ["Nâu sẫm"],
    material: "Da lộn",
    tags: ["90s Vintage"],
    image: "https://images.unsplash.com/photo-1551028719-00167b16eac5?w=500&auto=format&fit=crop&q=60"
  },
  {
    id: 2,
    name: "Quần jeans Levi's 501",
    sellType: "shop",
    sellerId: null,
    shopId: "SHOP_01",
    price: 650000,
    category: "Quần jeans",
    specs: { vongEo: 80, daiQuan: 104, rongOng: 21, rongDui: 58 },
    description: "Dáng đứng classic, xanh wash đẹp, không lỗi.",
    recommend: { height: "175-180 cm", weight: "70-80 kg" },
    brand: "Levi's",
    origin: "Mỹ",
    colors: ["Xanh nhạt"],
    material: "Denim",
    tags: ["80s Retro", "90s Vintage"],
    image: "https://images.unsplash.com/photo-1542272604-787c3835535d?w=500&auto=format&fit=crop&q=60"
  },
  {
    id: 3,
    name: "Áo thun ban nhạc Nirvana",
    sellType: "pass",
    sellerId: 2,
    shopId: null,
    price: 150000,
    category: "Áo thun",
    specs: { daiAo: 72, ngangVai: 54, ngangThan: 58, daiTay: 22 },
    description: "Hình in crack tự nhiên, vải mềm rũ.",
    recommend: { height: "165-175 cm", weight: "55-65 kg" },
    brand: "Gildan",
    origin: "Honduras",
    colors: ["Đen"],
    material: "Cotton",
    tags: ["90s Vintage"],
    image: "https://images.unsplash.com/photo-1521572163474-6864f9cf17ab?w=500&auto=format&fit=crop&q=60"
  },
  {
    id: 4,
    name: "Quần Parachute túi hộp",
    sellType: "shop",
    sellerId: null,
    shopId: "SHOP_01",
    price: 200000,
    category: "Quần dài",
    specs: { vongEo: 76, daiQuan: 102, rongOng: 26, rongDui: 64 },
    description: "Mặc đúng 1 lần, form thụng hack dáng.",
    recommend: { height: "160-170 cm", weight: "50-60 kg" },
    brand: "Unbranded",
    origin: "Trung Quốc",
    colors: ["Xám khói", "Bạc"],
    material: "Vải dù",
    tags: ["Y2K"],
    image: "https://images.unsplash.com/photo-1624378439575-d8705ad7ae80?w=500&auto=format&fit=crop&q=60"
  },
  {
    id: 5,
    name: "Chân váy mini xếp ly",
    sellType: "shop",
    sellerId: null,
    shopId: "SHOP_01",
    price: 180000,
    category: "Chân váy",
    specs: { vongEo: 64, daiVay: 38 },
    description: "Chân váy nữ sinh họa tiết kẻ caro đỏ.",
    recommend: { height: "150-160 cm", weight: "40-50 kg" },
    brand: "WEGO",
    origin: "Nhật Bản",
    colors: ["Đỏ", "Đen"],
    material: "Polyester",
    tags: ["Y2K"],
    image: "https://images.unsplash.com/photo-1574341902409-cf56487e35b7?w=500&auto=format&fit=crop&q=60"
  },
  {
    id: 6,
    name: "Áo sơ mi Flannel caro",
    sellType: "shop",
    sellerId: null,
    shopId: "SHOP_01",
    price: 250000,
    category: "Áo sơ mi",
    specs: { daiAo: 74, ngangVai: 48, ngangThan: 55, daiTay: 64 },
    description: "Sơ mi dạ nỉ dày dặn, không xù lông.",
    recommend: { height: "170-178 cm", weight: "65-75 kg" },
    brand: "Uniqlo",
    origin: "Việt Nam",
    colors: ["Đỏ", "Navy"],
    material: "Dạ nỉ",
    tags: ["90s Vintage"],
    image: "https://images.unsplash.com/photo-1596755094514-f87e32f85e2c?w=500&auto=format&fit=crop&q=60"
  },
  {
    id: 7,
    name: "Áo len vặn thừng Oversize",
    sellType: "shop",
    sellerId: null,
    shopId: "SHOP_01",
    price: 220000,
    category: "Áo len",
    specs: { daiAo: 70, ngangVai: 58, ngangThan: 62, daiTay: 55 },
    description: "Len dày cực ấm, phù hợp mùa đông.",
    recommend: { height: "155-165 cm", weight: "45-55 kg" },
    brand: "No Brand",
    origin: "Hàn Quốc",
    colors: ["Trắng kem"],
    material: "Len",
    tags: ["80s Retro"],
    image: "https://images.unsplash.com/photo-1620799140188-3b2a02fd9a77?w=500&auto=format&fit=crop&q=60"
  }
];

export const productsData = [
  ...baseProducts,
  ...baseProducts.map(p => ({ ...p, id: p.id + 10, name: p.name + " (Khác)", image: "https://images.unsplash.com/photo-1515886657613-9f3515b0c78f?w=500&auto=format&fit=crop&q=60" })),
  ...baseProducts.map(p => ({ ...p, id: p.id + 20, name: p.name + " (Mới)", image: "https://images.unsplash.com/photo-1434389678369-182cb14b0162?w=500&auto=format&fit=crop&q=60" }))
];

export const conversationsData = [
  {
    id: "CONV_01",
    participant1_Id: 1, // Anya
    participant2_Id: 3, // Yor (Shop Owner)
    relatedProductId: 22, // Quần jeans Levi's 501 (Mới)
    lastMessageAt: "2026-07-12T10:00:00Z"
  }
];

export const messagesData = [
  {
    id: "MSG_01",
    conversationId: "CONV_01",
    senderId: 1,
    messageType: "Text",
    content: "Shop ơi, quần này còn không ạ? Tớ cao 1m6 mặc vừa không?",
    createdAt: "2026-07-12T09:50:00Z"
  },
  {
    id: "MSG_02",
    conversationId: "CONV_01",
    senderId: 3,
    messageType: "Text",
    content: "Dạ quần còn ạ. Cao 1m6 mặc qua mắt cá xíu, đẹp lắm bạn nha!",
    createdAt: "2026-07-12T09:55:00Z"
  },
  {
    id: "MSG_03",
    conversationId: "CONV_01",
    senderId: 3,
    messageType: "DepositRequest",
    content: JSON.stringify({ amount: 50000, percentage: null, depositId: "DEP_01" }),
    createdAt: "2026-07-12T10:00:00Z"
  }
];

export const shopTagsData = [
  {
    id: "TAG_01",
    shopId: 3,
    tagName: "Khách VIP",
    colorCode: "#ef4444", // red
    createdAt: "2026-07-01T00:00:00Z"
  },
  {
    id: "TAG_02",
    shopId: 3,
    tagName: "Đã chốt",
    colorCode: "#22c55e", // green
    createdAt: "2026-07-01T00:00:00Z"
  }
];

export const conversationTagsData = [
  {
    conversationId: "CONV_01",
    tagId: "TAG_01"
  }
];

export const quickRepliesData = [
  {
    id: "QR_01",
    shopId: 3,
    shortcut: "/size",
    content: "Dạ áo này freesize dưới 60kg, eo dưới 80cm khách nhé!",
    sortOrder: 1
  },
  {
    id: "QR_02",
    shopId: 3,
    shortcut: "/ship",
    content: "Dạ phí ship nội thành là 20k, ngoại thành 30k ạ.",
    sortOrder: 2
  }
];

export const depositRequestsData = [
  {
    id: "DEP_01",
    conversationId: "CONV_01",
    productId: 22,
    requestedAmount: 50000,
    requestedPercentage: null,
    status: "Pending", // Pending, Accepted, Rejected
    expiresAt: "2026-07-13T10:00:00Z",
    linkedOrderId: null
  }
];
