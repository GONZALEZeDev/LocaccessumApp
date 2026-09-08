import { Outlet } from 'react-router-dom';
import { NavBar } from './NavBar';

export function AppLayout() {
  return (
    <div className="min-h-screen bg-primary-50">
      <NavBar />
      <main className="max-w-4xl mx-auto px-6 py-8">
        <Outlet />
      </main>
    </div>
  );
}
