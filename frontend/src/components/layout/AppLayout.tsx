import { Suspense, useState } from 'react';
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom';
import {
  AppBar, Avatar, Box, CircularProgress, Divider, Drawer, IconButton, List, ListItemButton, ListItemIcon, ListItemText,
  ListSubheader, Menu, MenuItem, Toolbar, Tooltip, Typography, useMediaQuery, useTheme
} from '@mui/material';
import MenuIcon from '@mui/icons-material/Menu';
import TranslateIcon from '@mui/icons-material/Translate';
import DarkModeOutlinedIcon from '@mui/icons-material/DarkModeOutlined';
import LightModeOutlinedIcon from '@mui/icons-material/LightModeOutlined';
import LogoutIcon from '@mui/icons-material/Logout';
import PersonOutlineIcon from '@mui/icons-material/PersonOutline';
import { useTranslation } from 'react-i18next';
import { useQuery } from '@tanstack/react-query';
import { api } from '@/api/client';
import type { CompanyProfileDto } from '@/api/generated';
import { navigation } from '@/app/navigation';
import { useColorMode } from '@/app/AppProviders';
import { useAuth } from '@/auth/AuthProvider';
import { setLanguage, useLanguage } from '@/i18n';

const DRAWER_WIDTH = 264;

function SideNav({ onNavigate }: { onNavigate?: () => void }) {
  const { t } = useTranslation();
  const { hasAny } = useAuth();
  const location = useLocation();

  // The most specific matching path is the active item (/shipments/new must not also light /shipments).
  const allPaths = navigation.flatMap((group) => group.items.map((item) => item.path));
  const active = allPaths
    .filter((path) => (path === '/' ? location.pathname === '/' : location.pathname === path || location.pathname.startsWith(`${path}/`)))
    .sort((a, b) => b.length - a.length)[0];

  return (
    <Box component="nav" sx={{ overflowY: 'auto', pb: 4 }}>
      {navigation.map((group) => {
        const items = group.items.filter((item) => !item.any || hasAny(...item.any));
        if (items.length === 0) {
          return null;
        }

        return (
          <List key={group.key} dense subheader={<ListSubheader sx={{ lineHeight: '32px', bgcolor: 'transparent' }}>{t(`nav.${group.key}`)}</ListSubheader>}>
            {items.map((item) => (
              <ListItemButton
                key={item.path}
                component={NavLink}
                to={item.path}
                end
                selected={active === item.path}
                onClick={onNavigate}
                sx={{ mx: 1, borderRadius: 1.5 }}
              >
                <ListItemIcon sx={{ minWidth: 36 }}>{item.icon}</ListItemIcon>
                <ListItemText primary={t(`nav.${item.key}`)} />
              </ListItemButton>
            ))}
          </List>
        );
      })}
    </Box>
  );
}

/** Shell: top bar (language, theme, user menu) + side menu (permanent on desktop, a drawer on phones). */
export default function AppLayout() {
  const theme = useTheme();
  const desktop = useMediaQuery(theme.breakpoints.up('md'));
  const [mobileOpen, setMobileOpen] = useState(false);
  const [userMenu, setUserMenu] = useState<HTMLElement | null>(null);
  const { t } = useTranslation();
  const { user, logout } = useAuth();
  const language = useLanguage();
  // Header shows the saved company name; before the profile exists (404) it falls back to the default text.
  const companyProfile = useQuery({ queryKey: ['/company-profile'], queryFn: () => api.get<CompanyProfileDto>('/company-profile'), staleTime: 5 * 60_000 });
  const colorMode = useColorMode();
  const navigate = useNavigate();

  const drawer = (
    <>
      <Toolbar sx={{ gap: 1 }}>
        <Box component="img" src="/favicon.svg" alt="" sx={{ width: 32, height: 32 }} />
        <Typography variant="h6" noWrap>
          {t('app.name')}
        </Typography>
      </Toolbar>
      <Divider />
      <SideNav onNavigate={desktop ? undefined : () => setMobileOpen(false)} />
    </>
  );

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh' }}>
      <AppBar position="fixed" color="inherit" elevation={0} sx={{ borderBottom: 1, borderColor: 'divider', width: { md: `calc(100% - ${DRAWER_WIDTH}px)` }, marginInlineStart: { md: `${DRAWER_WIDTH}px` } }}>
        <Toolbar sx={{ gap: 1 }}>
          {!desktop && (
            <IconButton edge="start" onClick={() => setMobileOpen(true)} aria-label="menu">
              <MenuIcon />
            </IconButton>
          )}
          <Typography variant="subtitle1" sx={{ flexGrow: 1, fontWeight: 600 }} noWrap>
            {(language === 'en' ? companyProfile.data?.englishName || companyProfile.data?.arabicName : companyProfile.data?.arabicName) || t('app.company')}
          </Typography>
          <Tooltip title={language === 'ar' ? 'English' : 'العربية'}>
            <IconButton onClick={() => setLanguage(language === 'ar' ? 'en' : 'ar')} aria-label={t('common.language')}>
              <TranslateIcon />
            </IconButton>
          </Tooltip>
          <Tooltip title={t('common.darkMode')}>
            <IconButton onClick={colorMode.toggle}>{colorMode.mode === 'dark' ? <LightModeOutlinedIcon /> : <DarkModeOutlinedIcon />}</IconButton>
          </Tooltip>
          <IconButton onClick={(event) => setUserMenu(event.currentTarget)} sx={{ p: 0.5 }}>
            <Avatar sx={{ width: 34, height: 34, bgcolor: 'primary.main', fontSize: 15 }}>{(user?.arabicFullName || user?.userName || '?').slice(0, 1)}</Avatar>
          </IconButton>
          <Menu anchorEl={userMenu} open={!!userMenu} onClose={() => setUserMenu(null)}>
            <Box sx={{ px: 2, py: 1 }}>
              <Typography fontWeight={700}>{language === 'ar' ? user?.arabicFullName : user?.fullName}</Typography>
              <Typography variant="caption" color="text.secondary">
                {user?.userName} · {user?.roles.join('، ')}
              </Typography>
            </Box>
            <Divider />
            <MenuItem onClick={() => { setUserMenu(null); navigate('/profile'); }}>
              <ListItemIcon><PersonOutlineIcon fontSize="small" /></ListItemIcon>
              {t('auth.profile')}
            </MenuItem>
            <MenuItem onClick={() => { setUserMenu(null); void logout(); }}>
              <ListItemIcon><LogoutIcon fontSize="small" /></ListItemIcon>
              {t('auth.signOut')}
            </MenuItem>
          </Menu>
        </Toolbar>
      </AppBar>

      <Box component="aside" sx={{ width: { md: DRAWER_WIDTH }, flexShrink: { md: 0 } }}>
        {desktop ? (
          <Drawer variant="permanent" open sx={{ '& .MuiDrawer-paper': { width: DRAWER_WIDTH, boxSizing: 'border-box' } }}>
            {drawer}
          </Drawer>
        ) : (
          <Drawer variant="temporary" open={mobileOpen} onClose={() => setMobileOpen(false)} ModalProps={{ keepMounted: true }} sx={{ '& .MuiDrawer-paper': { width: DRAWER_WIDTH } }}>
            {drawer}
          </Drawer>
        )}
      </Box>

      <Box component="main" sx={{ flexGrow: 1, minWidth: 0, p: { xs: 1.5, md: 3 }, pt: { xs: 9, md: 10 } }}>
        <Suspense fallback={<Box sx={{ display: 'grid', placeItems: 'center', py: 10 }}><CircularProgress /></Box>}>
          <Outlet />
        </Suspense>
      </Box>
    </Box>
  );
}
