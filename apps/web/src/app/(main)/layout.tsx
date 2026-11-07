import { SidebarProvider } from "@/components/ui/sidebar";
import { DynamicSidebar } from "@/components/dynamic-sidebar";
import { ThemeProvider } from "@/contexts/theme-context";
import { AuthGate } from "@/components/auth-gate";

export default function MainLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <ThemeProvider>
      <AuthGate>
        <SidebarProvider defaultOpen={true}>
          <DynamicSidebar />
          <main className="w-full">
            {children}
          </main>
        </SidebarProvider>
      </AuthGate>
    </ThemeProvider>
  );
}
