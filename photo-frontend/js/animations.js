// js/animations-light.js - Упрощенные анимации
(function() {
    function createFloatingElements() {
        const existingContainer = document.querySelector('.animated-icons-container');
        if (existingContainer) existingContainer.remove();
        
        const container = document.createElement('div');
        container.className = 'animated-icons-container';
        document.body.appendChild(container);
        
        // Только 3 камеры вместо 15
        const cameraIcons = ['📸', '📷', '🎥'];
        for (let i = 0; i < 3; i++) {
            const icon = document.createElement('div');
            icon.className = 'floating-camera';
            icon.innerHTML = cameraIcons[i % cameraIcons.length];
            icon.style.position = 'fixed';
            icon.style.fontSize = (25 + Math.random() * 15) + 'px';
            icon.style.left = Math.random() * 100 + '%';
            icon.style.top = Math.random() * 100 + '%';
            icon.style.animationDelay = (Math.random() * 15) + 's';
            icon.style.animationDuration = (15 + Math.random() * 15) + 's';
            icon.style.opacity = 0.08;
            container.appendChild(icon);
        }
        
        // Только 5 сердечек вместо 25
        const heartIcons = ['❤️', '💖', '💗', '💓', '💕'];
        for (let i = 0; i < 5; i++) {
            const icon = document.createElement('div');
            icon.className = 'floating-heart';
            icon.innerHTML = heartIcons[i % heartIcons.length];
            icon.style.position = 'fixed';
            icon.style.fontSize = (18 + Math.random() * 12) + 'px';
            icon.style.left = Math.random() * 100 + '%';
            icon.style.top = Math.random() * 100 + '%';
            icon.style.animationDelay = (Math.random() * 12) + 's';
            icon.style.animationDuration = (12 + Math.random() * 12) + 's';
            icon.style.opacity = 0.06;
            container.appendChild(icon);
        }
        
        // Только 5 звездочек вместо 20
        const starIcons = ['⭐', '🌟', '✨'];
        for (let i = 0; i < 5; i++) {
            const icon = document.createElement('div');
            icon.className = 'floating-star';
            icon.innerHTML = starIcons[i % starIcons.length];
            icon.style.position = 'fixed';
            icon.style.fontSize = (16 + Math.random() * 12) + 'px';
            icon.style.left = Math.random() * 100 + '%';
            icon.style.top = Math.random() * 100 + '%';
            icon.style.animationDelay = (Math.random() * 18) + 's';
            icon.style.animationDuration = (14 + Math.random() * 14) + 's';
            icon.style.opacity = 0.05;
            container.appendChild(icon);
        }
    }
    
    function addAnimationStyles() {
        if (document.getElementById('dynamic-animation-styles')) return;
        
        const style = document.createElement('style');
        style.id = 'dynamic-animation-styles';
        style.textContent = `
            @keyframes floatCamera {
                0%, 100% { transform: translate(0, 0) rotate(0deg); opacity: 0.06; }
                50% { transform: translate(15px, -20px) rotate(5deg); opacity: 0.12; }
            }
            @keyframes floatHeart {
                0%, 100% { transform: translate(0, 0) scale(1); opacity: 0.05; }
                50% { transform: translate(-10px, -15px) scale(1.05); opacity: 0.1; }
            }
            @keyframes floatStar {
                0%, 100% { transform: translate(0, 0) rotate(0deg); opacity: 0.04; }
                50% { transform: translate(10px, -15px) rotate(15deg); opacity: 0.08; }
            }
            .floating-camera, .floating-heart, .floating-star {
                position: fixed;
                pointer-events: none;
                z-index: 0;
            }
            .floating-camera { animation: floatCamera 20s ease-in-out infinite; }
            .floating-heart { animation: floatHeart 18s ease-in-out infinite; }
            .floating-star { animation: floatStar 22s ease-in-out infinite; }
            .animated-icons-container {
                position: fixed;
                top: 0;
                left: 0;
                width: 100%;
                height: 100%;
                pointer-events: none;
                z-index: 0;
                overflow: hidden;
            }
        `;
        document.head.appendChild(style);
    }
    
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function() {
            addAnimationStyles();
            createFloatingElements();
        });
    } else {
        addAnimationStyles();
        createFloatingElements();
    }
})();