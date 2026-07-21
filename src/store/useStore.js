import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import { 
  usersData as initialUsersData, 
  productsData,
  conversationsData,
  messagesData,
  shopTagsData,
  conversationTagsData,
  quickRepliesData,
  depositRequestsData
} from '../data/mockData';

const useStore = create(
  persist(
    (set, get) => ({
      users: initialUsersData,
      currentUser: null,
      
      login: (email, password) => {
        const user = get().users.find(u => u.email === email && u.password === password);
        if (user) {
          set({ currentUser: user });
          return true;
        }
        return false;
      },
      
      register: (userData) => {
        const users = get().users;
        if (users.find(u => u.email === userData.email)) {
          return false; // Email exists
        }
        const newUser = {
          ...userData,
          id: Date.now(),
          role: 'CasualUser',
          status: 'active',
          isAbleToSell: false,
          avatar: `https://via.placeholder.com/150/ffb6c1/ffffff?text=${userData.fullname.charAt(0)}`
        };
        set({ users: [...users, newUser] });
        return true;
      },

      logout: () => set({ currentUser: null }),

      checkoutProduct: null,
      setCheckoutProduct: (product) => set({ checkoutProduct: product }),

      orders: [],
      createOrder: (orderData) => set((state) => ({
        orders: [...state.orders, { ...orderData, id: Date.now() }]
      })),
      updateOrderStatus: (orderId, newStatus) => set((state) => ({
        orders: state.orders.map(order => 
          order.id === orderId ? { ...order, status: newStatus } : order
        )
      })),

      // --- CHAT STATES ---
      conversations: conversationsData,
      messages: messagesData,
      shopTags: shopTagsData,
      conversationTags: conversationTagsData,
      quickReplies: quickRepliesData,
      depositRequests: depositRequestsData,

      // --- CHAT ACTIONS ---
      sendMessage: (conversationId, senderId, messageType, content) => set((state) => {
        const newMessage = {
          id: 'MSG_' + Date.now(),
          conversationId,
          senderId,
          messageType,
          content,
          createdAt: new Date().toISOString()
        };
        
        const updatedConversations = state.conversations.map(c => 
          c.id === conversationId ? { ...c, lastMessageAt: newMessage.createdAt } : c
        );

        return { 
          messages: [...state.messages, newMessage],
          conversations: updatedConversations
        };
      }),

      createDepositRequest: (conversationId, productId, requestedAmount) => {
        const newDeposit = {
          id: 'DEP_' + Date.now(),
          conversationId,
          productId,
          requestedAmount,
          requestedPercentage: null,
          status: 'Pending',
          expiresAt: new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString(),
          linkedOrderId: null
        };
        set((state) => ({ depositRequests: [...state.depositRequests, newDeposit] }));
        return newDeposit;
      },

      updateDepositStatus: (depositId, newStatus, linkedOrderId = null) => set((state) => ({
        depositRequests: state.depositRequests.map(d => 
          d.id === depositId ? { ...d, status: newStatus, linkedOrderId } : d
        )
      })),

      createConversation: (participant1_Id, participant2_Id, relatedProductId) => {
        const state = get();
        let existing = state.conversations.find(c => 
          ((c.participant1_Id === participant1_Id && c.participant2_Id === participant2_Id) || 
           (c.participant1_Id === participant2_Id && c.participant2_Id === participant1_Id)) &&
          c.relatedProductId === relatedProductId
        );
        if (existing) return existing.id;

        const newId = 'CONV_' + Date.now();
        set((s) => ({
          conversations: [...s.conversations, {
            id: newId,
            participant1_Id,
            participant2_Id,
            relatedProductId,
            lastMessageAt: new Date().toISOString()
          }]
        }));
        return newId;
      }
    }),
    {
      name: 'doosii-storage', // name of the item in the storage (must be unique)
    }
  )
);

export default useStore;
