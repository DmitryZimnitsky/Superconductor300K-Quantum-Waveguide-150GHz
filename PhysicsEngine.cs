/*математическая модель, 
 * которая на каждом тике таймера рассчитывает положение атомов на частоте 150 ГГц, 
 * укрощая тепловой шум*/
using System;

namespace CristalWinForms
{
    public class PhysicsEngine
    {
        private const double M_Ta = 3.0e-25;       // Масса изотопа Тантал-181
        private const double K_TaSe = 150.0;       // Жесткость связи
        private const double Alpha = 5.0e14;       // Кубический ангармонизм
        private const double Gamma_Ba = 1.0e-12;   // Демпфирование Бария

        public double TargetFrequency { get; set; } = 150e9;
        public double Temperature { get; set; } = 300.0;

        private double _time = 0;
        private double _amplitude = 0;
        private double _velocity = 0;
        private readonly Random _rand = new Random();

        public double UpdatePhysics(double dt)
        {
            _time += dt;
            double thermalForce = Math.Sqrt(Temperature / 300.0) * (_rand.NextDouble() - 0.5) * 1e-10;
            double externalField = 5e-11 * Math.Cos(2 * Math.PI * TargetFrequency * _time);

            double force = -K_TaSe * _amplitude - Alpha * Math.Pow(_amplitude, 3)
                           - Gamma_Ba * _velocity + thermalForce + externalField;

            double acceleration = force / M_Ta;
            _velocity += acceleration * dt;
            _amplitude += _velocity * dt;

            // Замена Math.Clamp для .NET Framework 4.7.2:
            double max = 0.5;
            double min = -0.5;
            return _amplitude > max ? max : (_amplitude < min ? min : _amplitude);
        }
    }
}

