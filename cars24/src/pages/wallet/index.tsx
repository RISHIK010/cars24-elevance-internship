import { useAuth } from "@/context/AuthContext";
import { attachReferral, getReferralSummary } from "@/lib/referralapi";
import { getWallet, redeemWallet } from "@/lib/walletapi";
import {
  ArrowDownCircle,
  ArrowUpCircle,
  Copy,
  Gift,
  Wallet as WalletIcon,
} from "lucide-react";
import Link from "next/link";
import React, { useEffect, useState } from "react";
import { toast } from "sonner";

type ReferralItem = {
  id: string;
  referredUserId: string;
  status: string;
  rewardGranted: boolean;
  createdAt: string;
};

type ReferredBy = {
  id: string;
  referralCode: string;
  status: string;
  rewardGranted: boolean;
};

type Summary = {
  referralCode: string;
  referredBy: ReferredBy | null;
  rewardGranted: boolean;
  referrals: ReferralItem[];
};

type Transaction = {
  id: string;
  type: "credit" | "debit";
  amount: number;
  description: string;
  createdAt: string;
};

type WalletData = {
  balance: number;
  transactions: Transaction[];
};

const WalletPage = () => {
  const { user } = useAuth();
  const [summary, setSummary] = useState<Summary | null>(null);
  const [wallet, setWallet] = useState<WalletData | null>(null);
  const [loading, setLoading] = useState(true);
  const [attachCode, setAttachCode] = useState("");
  const [attaching, setAttaching] = useState(false);
  const [redeemAmount, setRedeemAmount] = useState("");
  const [redeeming, setRedeeming] = useState(false);

  useEffect(() => {
    if (!user) {
      setLoading(false);
      return;
    }
    const load = async () => {
      try {
        const [s, w] = await Promise.all([
          getReferralSummary(user.id),
          getWallet(user.id),
        ]);
        setSummary(s);
        setWallet(w);
      } catch (err) {
        console.error("Failed to load referral & wallet data:", err);
        toast.error("Failed to load referral & wallet data.");
      } finally {
        setLoading(false);
      }
    };
    load();
  }, [user]);

  const copyCode = async () => {
    if (!summary?.referralCode) return;
    try {
      await navigator.clipboard.writeText(summary.referralCode);
      toast.success("Referral code copied!");
    } catch {
      toast.error("Failed to copy referral code.");
    }
  };
  const handleAttach = async () => {
    if (!user || !attachCode.trim()) {
      toast.error("Please enter a referral code.");
      return;
    }
    setAttaching(true);
    try {
      await attachReferral(user.id, attachCode.trim());
      toast.success("Referral attached successfully.");
      setAttachCode("");
      const s = await getReferralSummary(user.id);
      setSummary(s);
    } catch (err: any) {
      toast.error(err?.message ?? "Failed to attach referral.");
    } finally {
      setAttaching(false);
    }
  };

  const handleRedeem = async () => {
    const amount = Number(redeemAmount);
    if (!user) return;
    if (!Number.isFinite(amount) || amount <= 0) {
      toast.error("Enter a valid amount greater than zero.");
      return;
    }
    setRedeeming(true);
    try {
      const key = `${user.id}-${Date.now()}-${Math.random().toString(36).slice(2)}`;
      await redeemWallet(user.id, amount, key);
      toast.success("Redemption successful!");
      setRedeemAmount("");
      const w = await getWallet(user.id);
      setWallet(w);
    } catch (err: any) {
      toast.error(err?.message ?? "Redemption failed.");
    } finally {
      setRedeeming(false);
    }
  };

  if (!user) {
    return (
      <div className="min-h-screen bg-gray-50 text-black flex items-center justify-center px-4">
        <div className="bg-white rounded-lg shadow-md p-8 text-center max-w-md">
          <Gift className="h-10 w-10 text-blue-600 mx-auto" />
          <h1 className="mt-4 text-xl font-bold">Referral & Wallet</h1>
          <p className="mt-2 text-gray-600">
            Please log in to view your referral and wallet details.
          </p>
          <Link
            href="/login"
            className="mt-4 inline-block bg-blue-600 text-white px-4 py-2 rounded-md hover:bg-blue-700"
          >
            Log in
          </Link>
        </div>
      </div>
    );
  }
  return (
    <div className="min-h-screen bg-gray-50 text-black">
      <main className="container mx-auto px-4 py-8 max-w-4xl">
        <h1 className="text-2xl font-bold mb-6">Referral & Wallet</h1>

        {loading ? (
          <p className="text-gray-500">Loading…</p>
        ) : (
          <div className="grid md:grid-cols-2 gap-6">
            {/* Referral section */}
            <section className="bg-white rounded-lg shadow-md p-6">
              <div className="flex items-center gap-2 mb-4">
                <Gift className="h-5 w-5 text-blue-600" />
                <h2 className="text-lg font-semibold">Referral</h2>
              </div>

              <div className="mb-4">
                <p className="text-sm text-gray-500">Your referral code</p>
                <div className="mt-1 flex items-center gap-2">
                  <span className="bg-blue-50 text-blue-700 font-mono font-bold text-xl px-3 py-1 rounded-md">
                    {summary?.referralCode ?? "—"}
                  </span>
                  <button
                    onClick={copyCode}
                    className="inline-flex items-center gap-1 text-sm text-blue-600 hover:text-blue-700"
                  >
                    <Copy className="h-4 w-4" />
                    Copy
                  </button>
                </div>
              </div>

              <div className="mb-4">
                <p className="text-sm text-gray-500">Referral status</p>
                {summary?.referredBy ? (
                  <p className="mt-1 text-sm">
                    Referred by{" "}
                    <span className="font-medium">{summary.referredBy.referralCode}</span>
                    {" — "}
                    <span
                      className={
                        summary.rewardGranted ? "text-green-600" : "text-amber-600"
                      }
                    >
                      {summary.rewardGranted ? "Reward earned" : "Reward pending"}
                    </span>
                  </p>
                ) : (
                  <p className="mt-1 text-sm text-gray-500">
                    No referrer yet. Enter a referral code below.
                  </p>
                )}
              </div>

              {!summary?.referredBy && (
                <div className="flex gap-2 mb-4">
                  <input
                    type="text"
                    value={attachCode}
                    onChange={(e) => setAttachCode(e.target.value)}
                    placeholder="Enter referral code"
                    className="flex-1 px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-blue-500 focus:border-blue-500 sm:text-sm"
                  />
                  <button
                    onClick={handleAttach}
                    disabled={attaching}
                    className="px-4 py-2 bg-blue-600 text-white rounded-md text-sm font-medium hover:bg-blue-700 disabled:opacity-50"
                  >
                    {attaching ? "Attaching…" : "Attach"}
                  </button>
                </div>
              )}

              <div>
                <p className="text-sm text-gray-500">People you referred</p>
                {summary?.referrals && summary.referrals.length > 0 ? (
                  <ul className="mt-2 space-y-1">
                    {summary.referrals.map((r) => (
                      <li
                        key={r.id}
                        className="flex items-center justify-between text-sm py-1 border-b border-gray-100"
                      >
                        <span className="text-gray-600 truncate">{r.referredUserId}</span>
                        <span
                          className={
                            r.rewardGranted ? "text-green-600" : "text-amber-600"
                          }
                        >
                          {r.rewardGranted ? "Rewarded" : "Pending"}
                        </span>
                      </li>
                    ))}
                  </ul>
                ) : (
                  <p className="mt-1 text-sm text-gray-500">
                    Share your code to start earning rewards.
                  </p>
                )}
              </div>
            </section>

            {/* Wallet section */}
            <section className="bg-white rounded-lg shadow-md p-6">
              <div className="flex items-center gap-2 mb-4">
                <WalletIcon className="h-5 w-5 text-blue-600" />
                <h2 className="text-lg font-semibold">Wallet</h2>
              </div>

              <div className="bg-gray-50 rounded-lg p-4 mb-4 text-center">
                <p className="text-sm text-gray-500">Available balance</p>
                <p className="text-3xl font-bold text-blue-700">
                  ₹{wallet?.balance?.toFixed(2) ?? "0.00"}
                </p>
              </div>

              <div className="mb-4">
                <p className="text-sm text-gray-500 mb-2">Redeem points</p>
                <div className="flex gap-2">
                  <input
                    type="number"
                    min="1"
                    step="0.01"
                    value={redeemAmount}
                    onChange={(e) => setRedeemAmount(e.target.value)}
                    placeholder="Amount"
                    className="flex-1 px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-blue-500 focus:border-blue-500 sm:text-sm"
                  />
                  <button
                    onClick={handleRedeem}
                    disabled={redeeming}
                    className="px-4 py-2 bg-orange-500 text-white rounded-md text-sm font-medium hover:bg-orange-600 disabled:opacity-50"
                  >
                    {redeeming ? "Redeeming…" : "Redeem"}
                  </button>
                </div>
              </div>

              <div>
                <p className="text-sm text-gray-500 mb-2">Transaction history</p>
                {wallet?.transactions && wallet.transactions.length > 0 ? (
                  <ul className="space-y-2 max-h-64 overflow-y-auto">
                    {wallet.transactions.map((t) => (
                      <li
                        key={t.id}
                        className="flex items-center justify-between text-sm border border-gray-100 rounded-md px-3 py-2"
                      >
                        <div className="flex items-center gap-2">
                          {t.type === "credit" ? (
                            <ArrowUpCircle className="h-4 w-4 text-green-600" />
                          ) : (
                            <ArrowDownCircle className="h-4 w-4 text-red-600" />
                          )}
                          <div>
                            <p className="font-medium">{t.description}</p>
                            <p className="text-xs text-gray-500">
                              {new Date(t.createdAt).toLocaleString()}
                            </p>
                          </div>
                        </div>
                        <span
                          className={
                            t.type === "credit"
                              ? "text-green-600 font-medium"
                              : "text-red-600 font-medium"
                          }
                        >
                          {t.type === "credit" ? "+" : "-"}₹{t.amount.toFixed(2)}
                        </span>
                      </li>
                    ))}
                  </ul>
                ) : (
                  <p className="text-sm text-gray-500">No transactions yet.</p>
                )}
              </div>
            </section>
          </div>
        )}
      </main>
    </div>
  );
};

export default WalletPage;



