export interface UserDetails {
  id: string;
  email: string;
  roles: string[];
}
export interface PortfolioDetails {
  id: number;
  accountId: number;
  name: string;
}
export interface Investment {
  id: number;
  symbol: string;
  name: string;
  currentPrice: number;
}
export interface Holding {
  investmentId: number;
  symbol: string;
  quantity: number;
  currentPrice: number;
  marketValue: number;
}
export interface Transaction {
  id: number;
  accountId: number;
  portfolioId: number | null;
  symbol: string | null;
  type: string;
  quantity: number | null;
  unitPrice: number | null;
  grossAmount: number;
  recordedAt: string;
}
export interface Dashboard {
  cashBalance: number;
  investmentValue: number;
  totalValue: number;
  accountCount: number;
  portfolioCount: number;
}

