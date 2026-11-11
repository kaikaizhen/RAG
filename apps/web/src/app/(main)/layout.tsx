import { SidebarProvider } from "@/components/ui/sidebar";
import { DynamicSidebar } from "@/components/dynamic-sidebar";
import { ThemeProvider } from "@/contexts/theme-context";
import { AuthGate } from "@/components/auth-gate";
import { BottomNav } from "@/components/mobile/bottom-nav";

export default function MainLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <ThemeProvider>
      <AuthGate>
        <SidebarProvider defaultOpen={true}>
          {/* 桌面版：側邊欄；手機版（含 PWA）：沒有側邊欄，改用底部分頁列 */}
          <DynamicSidebar />
          {/* 固定為視窗高度並自己捲動：頁面可以用 h-full 排版；手機版底部留出分頁列的空間（見 globals.css） */}
          <main className="mobile-main h-dvh min-w-0 flex-1 overflow-y-auto max-md:bg-muted/40">
            {children}
          </main>
          <BottomNav />
        </SidebarProvider>
      </AuthGate>
    </ThemeProvider>
  );
}
