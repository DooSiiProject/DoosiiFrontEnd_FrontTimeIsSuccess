# Doosii - Project Overview

## 1. Executive Summary
**Doosii** is a specialized Peer-to-Peer (P2P) and Shop-to-Consumer second-hand fashion platform designed for the Vietnamese market (starting with Ho Chi Minh City). Doosii tackles two major pain points in the thrift clothing community:
1. **Lack of Discovery & Physical Locality**: Traditional thrift shops and flea market stalls are hard to find and map out.
2. **Online Second-hand Fraud**: High scam rates, undisclosed clothing defects, and lack of secure transactions in P2P clothing pass groups.

Doosii bridges this gap through an interactive **Thrift Map** to locate nearby shops, an **Escrow (Ký quỹ)** transaction mechanism with holding and inspection windows, and a vibrant **Community Forum** for sharing outfits, thrift locations, and passing clothes safely.

---

## 2. Core Value Propositions
- **Safe P2P Escrow Guarantee**: Buyers deposit funds via VietQR (PayOS/SePay) which are temporarily locked in Doosii Escrow until the item is received, inspected, and accepted.
- **Thrift Map Discovery**: Locates flea market stalls, consignment chains, and boutique thrift shops in real time with radius filtering and Google Maps navigation.
- **Community-Driven Thrift Culture**: Style tagging (`Vintage`, `Y2K`, `Streetwear`, `Luxury/Brand`, `Retro`), spot-sharing, and direct in-app chat with 1-click escrow checkout.
- **Mega-Announcement Broadcasts**: Thrift shops can purchase bale-opening announcement broadcasts ("khui kiện") that send real-time SignalR push notifications to users within a 5km radius.

---

## 3. Key Actors & User Roles
1. **Guest**: Unauthenticated visitor. Can view public thrift maps, explore community posts, and browse available clothes.
2. **Customer (Buyer / Individual Seller)**: Default role upon registration. Can browse, buy items via Escrow, post personal clothes for clearance (pass đồ, up to 10 posts/24h), rate shops, and submit a Seller application.
3. **Seller (Shop / Store Owner)**: Verified via KYC approval (business license/front facade photo/ID card). Owns a physical shop marked on the Thrift Map, manages store inventory, fulfills escrow orders, purchases mega-announcements, and requests wallet payouts.
4. **Admin**: System operator. Approves/rejects Seller KYC requests, arbitrates escrow dispute cases (reviewing unboxing videos/evidence), moderates forum content, and monitors revenue statistics.

---

## 4. Key Functional Pillars
- **Auth & Profiles**: Email/Password with OTP verification, Google OAuth, BCrypt password hashing, JWT Access + Refresh tokens, user profiles and avatar storage on Cloudinary.
- **Thrift Map & Store Discovery**: Geolocation-based shop queries (Haversine / Spatial data), style and distance filtering, store profile, and directions.
- **Thrift Forum & Community**: Pass posts with 1–5 Cloudinary images, condition rating (% new), style tags, spot-sharing, comments, and in-app chat with Quick Escrow.
- **Escrow & Order Processing**: 15-minute product lock, dynamic VietQR payment, 48h shipping tracking window, 72h inspection window, 2-3% platform fee, dispute freeze and refund workflow.
- **Seller Tools & Admin Portal**: Inventory dashboard, 5km radius bale-opening announcements, wallet withdrawals, dispute arbitration, and audit logs.

---

## 5. Course & Project Roadmap (EXE201)
- **Phase 1: Planning & Design (Weeks 1–2)**: Finalize MVP feature list, UI/UX on Figma, database schemas, and architecture.
- **Phase 2: Core System Development (Weeks 3–6)**: Auth API, Database schemas (`auth`, `store`, `order`, `forum`, `notification`), Thrift Map API.
- **Phase 3: Application Integration (Weeks 7–10)**: React Frontend, PayOS Escrow integration, Seller Dashboard, SignalR notifications.
- **Phase 4: Testing & Security (Weeks 11–12)**: Functional testing, security audits for payments and unboxing dispute verification, bug fixes.
- **Phase 5: Launch & Operations (Week 13+)**: Beta testing and public launch in Ho Chi Minh City.
