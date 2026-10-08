import React, { useState } from "react";
import {
  Calendar,
  Package,
  Menu,
  Heart,
  User,
  ChevronDown,
  Wallet,
  X,
} from "lucide-react";
import Link from "next/link";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "./ui/dropdown-menu";
import { Button } from "./ui/button";
import { useAuth } from "@/context/AuthContext";
import NotificationControl from "./NotificationControl";
const Header = () => {
  const navItems = [
    { name: "Buy used car", href: "/buy-car" },
    { name: "Sell car", href: "/sell-car" },
  ];
  const menuItems = [
    { label: "My Appointments", icon: Calendar, link: "/appointments" },
    { label: "My Bookings", icon: Package, link: "/bookings" },
    { label: "Referral & Wallet", icon: Wallet, link: "/wallet" },
  ];
  // const user = {
  //   id: "1",
  //   avatar_url: "https://github.com/shadcn.png",
  //   email: "giris@gmail.com",
  //   full_name: "John Doe",
  //   phone: "+1234567890",
  //   created_at: new Date().toISOString(),
  // };
  const { user, signOut } = useAuth();
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  return (
    <header className="bg-white shadow-sm sticky top-0 z-50">
      <nav
        className="mx-auto flex max-w-7xl items-center justify-between p-4 lg:px-8"
        aria-label="Global"
      >
        <div className="flex lg:flex-1">
          <Link href="/" className="-m-1.5 p-1.5">
            <span className="sr-only">Cars24</span>
            <div className="flex items-center">
              <span className="bg-blue-600 text-white font-bold py-1 px-2 rounded-md text-lg">
                CARS
              </span>
              <span className="text-orange-500 font-bold text-lg">24</span>
            </div>
          </Link>
        </div>

        <div className="flex lg:hidden">
          <button
            type="button"
            aria-expanded={mobileMenuOpen}
            aria-controls="mobile-menu"
            className="-m-2.5 inline-flex items-center justify-center rounded-md p-2.5 text-gray-700"
            onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
          >
            <span className="sr-only">Toggle main menu</span>
            {mobileMenuOpen ? (
              <X className="h-6 w-6" aria-hidden="true" />
            ) : (
              <Menu className="h-6 w-6" aria-hidden="true" />
            )}
          </button>
        </div>
        <div className="hidden lg:flex lg:gap-x-8">
          {navItems.map((item) => (
            <div key={item.name} className="relative group">
              <Link
                href={item.href}
                className="flex items-center text-sm font-medium text-gray-900 hover:text-blue-600"
              >
                {item.name}
              </Link>
            </div>
          ))}
        </div>
        <div className="hidden lg:flex lg:flex-1 lg:justify-end items-center space-x-4">
          <Button
            variant="ghost"
            size="sm"
            className="text-sm text-gray-700 hover:text-blue-600"
          >
            <Heart className="mr-1 h-4 w-4" />
            <span>Wishlist</span>
          </Button>
          <NotificationControl />
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button
                variant="ghost"
                className="inline-flex items-center justify-center px-4 py-2 text-sm font-medium text-gray-700 hover:text-orange-500"
              >
                {user ? (
                  <>
                    <div className="w-8 h-8 rounded-full bg-gray-200 flex items-center justify-center overflow-hidden">
                      {user?.fullName ? (
                        <div className="w-full h-full bg-gray-200 text-gray-700 flex items-center justify-center text-sm font-medium uppercase">
                          {user.fullName.charAt(0)}
                        </div>
                      ) : (
                        <User className="w-5 h-5 text-gray-500" />
                      )}
                    </div>
                    <span className="ml-2">{user.fullName}</span>
                  </>
                ) : (
                  <>
                    <span>Account</span>
                    <ChevronDown className="ml-1 h-4 w-4" />
                  </>
                )}
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="w-72">
              {user ? (
                <>
                  <DropdownMenuItem asChild>
                    <Link
                      href="/profile"
                      className="w-full flex items-center gap-2"
                    >
                      Profile Settings
                    </Link>
                  </DropdownMenuItem>

                  <DropdownMenuItem
                    className="text-red-600 focus:bg-muted"
                    onClick={signOut}
                  >
                    Sign Out
                  </DropdownMenuItem>

                  <DropdownMenuSeparator />
                </>
              ) : (
                <>
                  <DropdownMenuItem asChild>
                    <Link
                      href="/login"
                      className="w-full px-4 py-3 text-center text-white bg-orange-500 rounded-md hover:bg-orange-600 transition-colors"
                    >
                      LOG&nbsp;IN / SIGN&nbsp;UP
                    </Link>
                  </DropdownMenuItem>

                  <DropdownMenuSeparator />
                </>
              )}

              {/* Common menu items */}
              {menuItems.map(({ label, icon: Icon, link }) => (
                <DropdownMenuItem asChild key={label}>
                  <Link href={link} className="flex items-center gap-3 w-full">
                    <Icon className="h-4 w-4 text-muted-foreground" />
                    {label}
                  </Link>
                </DropdownMenuItem>
              ))}
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
      </nav>

      {mobileMenuOpen && (
        <div
          id="mobile-menu"
          className="lg:hidden border-t border-gray-200 bg-white"
        >
          <div className="space-y-1 px-4 pb-4 pt-2">
            {navItems.map((item) => (
              <Link
                key={item.name}
                href={item.href}
                onClick={() => setMobileMenuOpen(false)}
                className="block rounded-md px-3 py-2 text-base font-medium text-gray-900 hover:bg-gray-50 hover:text-blue-600"
              >
                {item.name}
              </Link>
            ))}
            {menuItems.map(({ label, icon: Icon, link }) => (
              <Link
                key={label}
                href={link}
                onClick={() => setMobileMenuOpen(false)}
                className="flex items-center gap-3 rounded-md px-3 py-2 text-base font-medium text-gray-900 hover:bg-gray-50 hover:text-blue-600"
              >
                <Icon className="h-5 w-5 text-gray-500" />
                {label}
              </Link>
            ))}
            <div className="my-2 border-t border-gray-200" />
            {user ? (
              <>
                <Link
                  href="/profile"
                  onClick={() => setMobileMenuOpen(false)}
                  className="flex items-center gap-3 rounded-md px-3 py-2 text-base font-medium text-gray-900 hover:bg-gray-50"
                >
                  <User className="h-5 w-5 text-gray-500" />
                  Profile Settings
                </Link>
                <button
                  type="button"
                  onClick={() => {
                    setMobileMenuOpen(false);
                    signOut();
                  }}
                  className="w-full rounded-md px-3 py-2 text-left text-base font-medium text-red-600 hover:bg-gray-50"
                >
                  Sign Out
                </button>
              </>
            ) : (
              <Link
                href="/login"
                onClick={() => setMobileMenuOpen(false)}
                className="flex items-center gap-3 rounded-md px-3 py-2 text-base font-medium text-gray-900 hover:bg-gray-50"
              >
                <User className="h-5 w-5 text-gray-500" />
                Login / Sign Up
              </Link>
            )}
          </div>
        </div>
      )}
    </header>
  );
};

export default Header;
